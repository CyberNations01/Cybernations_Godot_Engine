#include "game/GameUtility.hpp"
#include <sstream>
#include <algorithm>
#include <optional>

namespace {
    void clearActiveDisruption(GameState& state) {
        state.activeDisruption = std::nullopt;
    }

    void applyResource(GameState& state, const std::string& r) {
        if (r == "HR")        state.params.adjustParam(CyberParameter::HUMAN_RELATION, 1);
        else if (r == "Tech") state.params.adjustParam(CyberParameter::TECHNOLOGY, 1);
        else if (r == "Env")  state.params.adjustParam(CyberParameter::ENVIRONMENT, 1);
        else if (r == "-Co")  state.params.adjustParam(CyberParameter::COHESION, -1);
    }

}  // namespace




nlohmann::json 
GameUtility::pathResultToJson(const int& tile, const int& side, 
                const std::vector<std::string> & resources, 
                const std::string& layer)
{
    nlohmann::json data;
    nlohmann::json res = nlohmann::json::array();

    data["tile"] = tile;
    data["side"] = side;
    data["layer"] = layer;
    for (const auto& r : resources)
        res.push_back(r);
    data["resources"] = res;
    return data;
}

ActionResult GameUtility::walkPath(GameState &state)
{
    // ! { Tile, side } 
    std::vector<Tile> gameBoard = state.getBoard();
    std::pair<int, int> tokenLocation = state.getPeopleToken();
    Tile currentTile;
    std::vector<std::string> resourceClaimed;
    nlohmann::json resJson = nlohmann::json::array();


    while (true) {

        currentTile = gameBoard[tokenLocation.first];
        Stack baseStack = currentTile.getBase();
        Stack overlayStack;
        Stack travereStack = baseStack;
        int connectedSide;
        std::vector<std::string> resources;

        std::cout << "Ppl token at: " 
                  << "Tile: " << tokenLocation.first << " " 
                  << "Side: " << tokenLocation.second << std::endl;
        
        /* ! Use path on Overlay Stack if exists */
        if (currentTile.hasOverlay()) {
            overlayStack = gameBoard[tokenLocation.first].getOverlay();
            travereStack = overlayStack;
        }


        const int entryBoardSide = tokenLocation.second;
        const int entryStackSide = currentTile.boardSideToStackSide(entryBoardSide);
        connectedSide = travereStack.getConnectedSide(entryStackSide);
        if (connectedSide == -1) 
            break;
        const int exitBoardSide = currentTile.stackSideToBoardSide(connectedSide);
        
        /* 2. Resource Collection 
            - If Overlay Stack exist, collect both stack resources
            - Collect resources for both of the connected sides 
        */ 
        auto collectAndApply = [&](const Stack& stack, int stackSide, int boardSide, const std::string& layer) {
            const auto& resources = stack.getSides()[stackSide];
            resJson.push_back(pathResultToJson(tokenLocation.first, boardSide, resources, layer));
            for (const auto& r : resources)
                applyResource(state, r);
        };

        collectAndApply(baseStack, entryStackSide, entryBoardSide, "base");
        collectAndApply(baseStack, connectedSide, exitBoardSide, "base");

        if (currentTile.hasOverlay()) {
            collectAndApply(overlayStack, entryStackSide, entryBoardSide, "overlay");
            collectAndApply(overlayStack, connectedSide, exitBoardSide, "overlay");
        }

        /*! 3. Update tokenLocation */ 
        std::pair<int,int> next = currentTile.getNeighbours()[exitBoardSide];
        if (next.first < 0  || next.second < 0 ||
            next.first  >= GameState::NUM_TILE   || 
            next.second >= Tile::TILE_SIDES) {
                tokenLocation.second = exitBoardSide;
                break;
        }

        tokenLocation = next;
    }
    if (state.setPeopleToken(tokenLocation))
        return ActionResult::success(ActionMessage("walkPath", resJson.dump()));
    else 
        return {ActionStatus::INVALID_ACTION, {"walkPath", "Set People Token failed"}};
}

ActionResult GameUtility::drawDisruption(GameState& state)
{
    if (state.activeDisruption.has_value())
        return {ActionStatus::INVALID_ACTION, 
               {"draw_disruption", 
                "Disruption Card has already been drawn"}};
    state.activeDisruption = state.disruptionManager.draw();
    return {ActionStatus::SUCCESS, {
        "draw_disruption",
        "Disruption Card is drawn"
    }};
}

bool GameUtility::checkResourceCondition(GameState& state, ResourceCondition cond)
{
    int lhs = state.params.getParamAmount(cond.lhs);
    int rhs = state.params.getParamAmount(cond.rhs);
    return compareWithOp(lhs, cond.compare, rhs);
}

void GameUtility::resolveParamEffect(GameState & state,
                                     const std::pair<DisruptionEffect, int>& effect)
{
    int delta = effect.second; 
    switch (effect.first) {
        case DisruptionEffect::COHESION:
            if (delta < 0 && state.ignoreCohesionLossThisRound)
                break;
            state.params.adjustParam(CyberParameter::COHESION, delta);
            break;
        case DisruptionEffect::HUMAN_RELATION:
            state.params.adjustParam(CyberParameter::HUMAN_RELATION, delta);
            break;
        case DisruptionEffect::CYBERNATION:
            state.params.adjustParam(CyberParameter::CYBERNATION_LEVEL, delta);
            break;
        case DisruptionEffect::TECHNOLOGY:
            state.params.adjustParam(CyberParameter::TECHNOLOGY, delta);
            break;
        case DisruptionEffect::ENVIRONMENT:
            state.params.adjustParam(CyberParameter::ENVIRONMENT, delta);
            break;
        default:
            break;
    }
}

void GameUtility::changeTileStack(GameState& state, 
                                  int tilePos,
                                  StackType targetType)
{
    CardManager<Stack>* StackManager;
    switch (targetType) {
        case StackType::WILD:
             StackManager = &state.wildStackManager;

            if (state.board[tilePos].hasOverlay())
                state.board[tilePos].removeOverlay();

            if (StackManager->isDeckEmpty())
                StackManager->reshuffle();
            
            if (state.board[tilePos].getBase().getType() != StackType::WILD)
                state.board[tilePos].setBase(StackManager->draw());

            break;
        
        case StackType::WASTE:
            StackManager = &state.wasteStackManager;

            if (StackManager->isDeckEmpty())
                StackManager->reshuffle();
            
            if (state.board[tilePos].hasOverlay())
                state.board[tilePos].removeOverlay();
            
            if (state.board[tilePos].getBase().getType() != StackType::WASTE)
                state.board[tilePos].setBase(StackManager->draw());
            
            break;
        
        case StackType::DEV_A:
            StackManager = &state.devAStackManager;
            
            if (StackManager->isDeckEmpty())
                StackManager->reshuffle();
            
            if (!state.board[tilePos].hasOverlay())
                state.board[tilePos].setOverlay(StackManager->draw());

            else if (state.board[tilePos].getEffectiveType() != StackType::DEV_A)
                state.board[tilePos].setOverlay(StackManager->draw());

            break;
        case StackType::DEV_B:
            StackManager = &state.devBStackManager;
            
            if (StackManager->isDeckEmpty())
                StackManager->reshuffle();
            
            if (!state.board[tilePos].hasOverlay())
                state.board[tilePos].setOverlay(StackManager->draw());

            else if (state.board[tilePos].getEffectiveType() != StackType::DEV_B)
                state.board[tilePos].setOverlay(StackManager->draw());


            break;
        default:
            break;

    }
}

void GameUtility::applyDisruptionStackOrParamOnTile(GameState& state, int tilePos,
                                                    const std::pair<DisruptionEffect, int>& e)
{
    switch (e.first) {
        case DisruptionEffect::TURN_WILD:
            changeTileStack(state, tilePos, StackType::WILD);
            return;
        case DisruptionEffect::TURN_WASTE:
            changeTileStack(state, tilePos, StackType::WASTE);
            return;
        case DisruptionEffect::TURN_DEV_A:
            changeTileStack(state, tilePos, StackType::DEV_A);
            return;
        case DisruptionEffect::TURN_DEV_B:
            changeTileStack(state, tilePos, StackType::DEV_B);
            return;
        default:
            resolveParamEffect(state, e);
            return;
    }
}


static std::vector<int> parseIntList(const std::string& s)
{
    std::vector<int> result;
    std::istringstream ss(s);
    std::string token;
    while (std::getline(ss, token, ',')) {
        result.push_back(std::stoi(token));
    }
    return result;
}

namespace {

void payResourceCostDelta(GameState& state, DisruptionEffect e, int delta)
{
    if (e == DisruptionEffect::COHESION && delta < 0 && state.ignoreCohesionLossThisRound)
        return;
    auto p = tryDisruptionEffectToCyberParameter(e);
    if (p.has_value())
        state.params.adjustParam(*p, delta);
}

}  // namespace

/**
 * cancelOnTiles: Let player pay cost to skip card effects on chosen tiles
 * 
 * @state: Game State (modified if cancel cost is paid)
 * @card: Disruption card being resolved
 * @action: Client request, reads "canceltiles" param (e.g. "1,3")
 * @effectiveTiles: [out] tiles NOT cancelled, to be affected by card effects
 * 
 * If client sends no "canceltiles", all card targets go into effectiveTiles.
 * If client cancels some tiles, deducts the cancel cost and returns the rest.
 * 
 * Returns std::nullopt on success, ActionResult on failure.
 */
std::optional<ActionResult> 
GameUtility::cancelOnTiles(GameState & state,
                           const DisruptionCard& card,
                           const Action &action,
                           std::vector<int>& effectiveStackTarget)
{

    const auto &clientReq = action.params;
    const auto &stackTarget = card.getStackTargets();


    if (clientReq.find("canceltiles") == clientReq.end()) {
        for (auto t : stackTarget)
            effectiveStackTarget.push_back(t);
        return std::nullopt;
    }

    if (!card.isCancellable())
        return ActionResult::invalid("Card is not cancellable\n");

    auto list = parseIntList(clientReq.at("canceltiles"));
    if (list.empty())
        return ActionResult::invalid(
            "canceltiles cannot be empty — omit the canceltiles field entirely to accept the full effect "
            "on all targets without paying cancel costs");

    for (int ct : list) {
        if (std::find(stackTarget.begin(), stackTarget.end(), ct) == stackTarget.end())
            return ActionResult::invalid("canceltiles must only list tiles from this card's stackTarget");
    }
    std::set<int> cancelTiles = std::set<int>(list.begin(), list.end());

    for (auto t : stackTarget) {
        if (cancelTiles.find(t) == cancelTiles.end())
            effectiveStackTarget.push_back(t);
    }

    std::pair<DisruptionEffect, int> costPair = card.getCosts()[0];
    int costOnCancel = (stackTarget.size() - effectiveStackTarget.size()) * costPair.second;
    auto costParamOpt = tryDisruptionEffectToCyberParameter(costPair.first);
    if (!costParamOpt.has_value())
        return ActionResult::invalid(
            "Cancel cost type on this card is not a resource (use Cy/Co/HR/Tech/Env in disruption.json cost)");

    if (state.params.getParamAmount(*costParamOpt) < std::abs(costOnCancel))
        return ActionResult::invalid("Not enough resources to cancel");

    payResourceCostDelta(state, costPair.first, costOnCancel);

    return std::nullopt;
}

/** 
 * filterTilesByStackCondition: Extract tiles that meet card stack condition
 * 
 * @state: Game State
 * @tiles: Source tiles list
 * @card: Disruption Card
 * 
 * Returns filtered tiles list
 */
std::vector<int> GameUtility::filterTilesByStackCondition(GameState &state,
                                                          const std::vector<int>& tiles,
                                                          const DisruptionCard& card)
{
    if (card.getConditionType() != ConditionType::STACK || !card.getStackCondition().has_value())
        return tiles;

        const auto& allow = card.getStackCondition().value().stackTypes;
        std::vector<int> filtered;
        for (const int t : tiles) {
            if (t < 0 || t >= static_cast<int>(state.board.size())) continue;
            StackType cur = state.board[t].getEffectiveType();
            for (const auto st : allow) {
                if (cur == st) {
                    filtered.push_back(t);
                    break;
                }
            }
        }
    return filtered;
}

/** 
 * cancelCard: Apply cancel cost on Game state
 * 
 * @state: Game State (modified if cancel cost is paid)
 * @action:  Client request, reads "cancel" param (e.g. "1")
 * @card: Disruption Card
 * 
 * If client sends "cancel", cancel costs are applied to @state
 * 
*/
std::optional<ActionResult> GameUtility::cancelCard(GameState &state, 
                                                    const Action & action,
                                                    const DisruptionCard &card)
{
    const auto& clientReq = action.params;
    if ((clientReq.find("cancel") != clientReq.end()) &&
        (clientReq.at("cancel") == "1")) {
            for (auto e : card.getCosts()) {
                if (state.params.getParamAmount(disruptionEffectToCyberParameter(e.first)) 
                    > std::abs(e.second))
                    resolveParamEffect(state, e);
                else
                    return ActionResult::invalid("Not enough resources to cancel");
            }
        clearActiveDisruption(state);
        return ActionResult::success(ActionMessage("resolve_disruption", "Disruption is resolved"));
    }
    return std::nullopt;

}


ActionResult GameUtility::applyDisruptionEffect(GameState& state, const Action& action)
{
    if (!state.activeDisruption.has_value())
        return ActionResult::invalid("Disruption card is not drawn");

    // ! Disruption Card  
    DisruptionCard &card = state.activeDisruption.value();
    std::string category = card.getCategory();
    const auto& tiles = card.getStackTargets();
    std::vector<std::pair<DisruptionEffect, int>> cancelCosts = card.getCosts();
    std::vector<int> effectiveStackTarget;
    const auto& clientReq = action.params;


    if (category == "CatA") {
        auto err = cancelOnTiles(state, card, action, effectiveStackTarget);
        if (err.has_value())
            return err.value();
    }
    
    /* CatB */
    else if (category == "CatB") {

        if (card.getConditionType() == ConditionType::NONE) {
            auto ret = cancelCard(state, action, card);
            if (ret.has_value())
                return ret.value();

            for (auto e : card.getEffects())
                resolveParamEffect(state, e);
        }

        else if (card.getConditionType() == ConditionType::STACK) {
            auto err = cancelOnTiles(state, card, action, effectiveStackTarget); 
            if (err.has_value())
                return err.value();
            
            effectiveStackTarget = filterTilesByStackCondition(state, effectiveStackTarget, card);
            for (int t : effectiveStackTarget) {
                for (const auto &e: card.getEffects())
                    applyDisruptionStackOrParamOnTile(state, t, e);
            }
            state.rebuildTokenBag();
            effectiveStackTarget.clear(); // applied above; skip duplicate pass at end of applyDisruptionEffect
        }

        else if (card.getConditionType() == ConditionType::RESOURCE) {
            if (card.getResourceCondition().has_value() &&
                checkResourceCondition(state, card.getResourceCondition().value())) {
                auto ret = cancelCard(state, action, card);
                if (ret.has_value())
                    return ret.value();
                for (const auto& e : card.getEffects())
                    resolveParamEffect(state, e);
            }
        }
    }

    /* CatC, CatD */
    else if (category == "CatC" || category == "CatD") {
        auto err = cancelOnTiles(state, card, action, effectiveStackTarget);
        if (err.has_value())
            return err.value();

        effectiveStackTarget = filterTilesByStackCondition(state, effectiveStackTarget, card);
    }

    /* Cat E: Swap or draw goal */
    else if (category == "CatE") {
        if (clientReq.find("cancel") != clientReq.end())
            state.currentGoal = state.goalManager.draw();
        else {
            for (const auto &e: card.getCosts()) {
                if (state.params.getParamAmount(disruptionEffectToCyberParameter(e.first))
                    < std::abs(e.second)) {
                    return ActionResult::invalid("Not enough resources to cancel");
                }
            }

            for (const auto &e: card.getCosts())
                resolveParamEffect(state, e);
        }
    }

    /* Cat F */
    else if (category == "CatF") {
        for (const auto& e : card.getEffects()) {
            if (e.first == DisruptionEffect::RESOURCES) {
                int limit = e.second;
                int total = 0;
                std::vector<std::pair<DisruptionEffect, int>> resourcesList;

                /* 
                 * Acquire Resources amount from client requests 
                 * (i.e. {"HR": "2", "Tech": 2, "Env": 1})
                 */
                for (const auto &key :{"HR", "Tech", "Env"}) {
                    auto it = clientReq.find(key);
                    if (it == clientReq.end()) continue;
                    int val = std::stoi(it->second);
                    
                    if (val < 0)
                        return ActionResult::invalid("Resource distribution values must be >= 0");
                    total += val;
                    resourcesList.push_back({strToDisruptionEffect(key), val});
                }

                /* Request resource should be bounded by limit */
                if (total > limit)
                    return ActionResult::invalid("Resource distribution must be <= " + std::to_string(limit));

                for (const auto &e : resourcesList)
                    resolveParamEffect(state, e);
            } else
                resolveParamEffect(state, e);
        }
    }

    /* Cat G */
    else if (category == "CatG") {
        std::vector<int> filteredStack = filterTilesByStackCondition(state, card.getStackTargets(), card);

        /* No action if no stack meet card criteria */
        if (filteredStack.size() == 0) {
            clearActiveDisruption(state);
            return ActionResult::success(ActionMessage("resolve_disruption", "No applicable stack"));
        }

        /* Extract "effectIndex" from client request*/
        if (clientReq.find("effectIndex") == clientReq.end())
            return ActionResult::invalid("CatG requires effectIndex");
        
        std::vector<int> effectIndex = parseIntList(clientReq.at("effectIndex"));
        if (effectIndex.size() < filteredStack.size())
            return ActionResult::invalid("effectIndex size must match filtered stack count");

        /* Apply selected effect */
        const auto &cardEffect = card.getEffects();
        for (auto e : effectIndex) {
            if (e < 0 || e >= static_cast<int>(cardEffect.size()))
                return ActionResult::invalid("Invalid effectIndex value");
            else
                resolveParamEffect(state, cardEffect[e]);
        }
    }

    /* CatH */
    else if (category == "CatH") {
        
        /* Extract "effectIndex" from client request */
        if (clientReq.find("effectIndex") == clientReq.end())
            return ActionResult::invalid("CatH requires effectIndex");
        
        int effectIndex = std::stoi(clientReq.at("effectIndex"));
        auto cardEffect = card.getEffects();
        if (effectIndex < 0 || effectIndex >= static_cast<int>(cardEffect.size()))
            return ActionResult::invalid("Invalid effectIndex value");
        
        /* Special Effect: MovePpl, Token, IgnoreCohesionEffect */
        switch (cardEffect[effectIndex].first) {
            case DisruptionEffect::IGNORE_COHESION_EFFECT:
                state.ignoreCohesionLossThisRound = true;
                break;

            // TODO ! Draw token and Put into TokenBag
            case DisruptionEffect::TOKEN:
                break;
            
            case DisruptionEffect::MOVE_PEOPLE: {
                if (clientReq.find("ppl") == clientReq.end())
                    return ActionResult::invalid("ppl token location required");
                auto location = parseIntList(clientReq.at("ppl"));
                if (location.size() < 2)
                    return ActionResult::invalid("Invalid ppl token location");
                state.setPeopleToken({location[0], location[1]});
                break;
            }
                
            default:
                resolveParamEffect(state, cardEffect[effectIndex]);
                break;
        }
    }

    /* 
     * CatI: Build on undeveloped Tile
     * 1. Get "targetTiles" from client request
     * 2. Check whether target tiles are "undeveloped"
     * 3. Check resource available
     * 4. Apply Cost
     */
    else if (category == "CatI") {
        if (clientReq.find("targetTiles") == clientReq.end()) {
            clearActiveDisruption(state);
            return ActionResult::success(ActionMessage("resolve_disruption", "No development"));
        }
        
        std::vector<int> targetTiles = parseIntList(clientReq.at("targetTiles"));

        // ! Check whether target tiles are "undeveloped"
        for (auto e: targetTiles) {
            if (e < 0 || e > GameState::NUM_TILE)
                return ActionResult::invalid("Invalid targetTile");
            if (state.board[e].hasOverlay())
                return ActionResult::invalid("Cannot build on developed tile");
        }
        std::vector<std::pair<DisruptionEffect, int>> costToApply;
        for (auto c : card.getCosts()) {
            auto targetParamLevel = state.params.getParamAmount(disruptionEffectToCyberParameter(c.first));
            auto cost = c.second * targetTiles.size();
            if (targetParamLevel < cost)
                return ActionResult::invalid("Not enough resources for CatI cost");
            costToApply.push_back({c.first, cost});
        }

        for (auto e : costToApply)
            resolveParamEffect(state, e);
        
        effectiveStackTarget = targetTiles;
    }

    /* CatJ */
    else if (category == "CatJ") {
        effectiveStackTarget = filterTilesByStackCondition(state, card.getStackTargets(), card);

        /* Extract "useOptional" from client request */
        auto it = clientReq.find("useOptional");

        if (it != clientReq.end() && it->second == "1") {
            auto optionalCosts = card.getOptionalCosts();
            for (const auto& c: optionalCosts) {
                if (state.params.getParamAmount(disruptionEffectToCyberParameter(c.first)) 
                    < c.second)
                    return ActionResult::invalid("Not enough resources for CatJ optional cost");
            }

            /* Apply optional cost and gain */
            for (const auto& c: optionalCosts)
                resolveParamEffect(state, c);
            
            for (const auto& g : card.getOptionalGains())
                resolveParamEffect(state, g);
        }
    }


    /* CatK : Trade Resources */
    else if (category == "CatK") {
        return tradeForDisruption(state, action);
    }

    /* Apply stack & tile effect */
    if (!effectiveStackTarget.empty()) {
        for (auto t : effectiveStackTarget) {
            for (const auto& e : card.getEffects())
                GameUtility::applyDisruptionStackOrParamOnTile(state, t, e);
        }
        state.rebuildTokenBag();
    }

    clearActiveDisruption(state);
    return ActionResult::success(ActionMessage("resolve_disruption", "Disruption is resolved"));
}

/** 
 * Trade Resource
 */
ActionResult GameUtility::tradeForDisruption(GameState& state, const Action& action)
{
    auto& req = action.params;

    auto srcIt = req.find("src");
    auto dstIt = req.find("dst");
    if (srcIt == req.end() || dstIt == req.end())
        return ActionResult::invalid("trade requires src and dst");

    CyberParameter src, dst;
    if (!parseCyberParameter(srcIt->second, src) ||
        !parseCyberParameter(dstIt->second, dst))
        return ActionResult::invalid("Unknown trade parameter");

    if (src == dst)
        return ActionResult::invalid("src and dst must differ");

    if (src == CyberParameter::COHESION || dst == CyberParameter::COHESION)
        return ActionResult::invalid("Cohesion cannot be traded");

    int amount = 1;
    auto amtIt = req.find("amount");
    if (amtIt != req.end())
        amount = std::stoi(amtIt->second);

    if (amount <= 0)
        return ActionResult::invalid("Trade amount must be > 0");

    if (state.params.getParamAmount(src) < amount)
        return ActionResult::invalid("Not enough resource to trade");

    state.params.adjustParam(src, -amount);
    state.params.adjustParam(dst, amount);

    clearActiveDisruption(state);
    return ActionResult::success(ActionMessage("trade", "Trade completed"));
}