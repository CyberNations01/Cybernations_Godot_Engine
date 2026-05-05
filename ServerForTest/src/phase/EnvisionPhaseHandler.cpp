#include "phase/EnvisionPhaseHandler.hpp"

ActionResult EnvisionPhaseHandler::handle(const Action& action, GameState& state)
{
    // "pass" is valid in every phase — RoundController handles the pass 
    // mechanics, but we still return SUCCESS so it knows to proceed.

    if (action.type == "shift_power")
        return handleShiftPower(action, state);

    if (action.type == "come_together")
        return handleComeTogether(action, state);

    if (action.type == "prepare")
        return handlePrepare(action, state);

    if (action.type == "set_course")
        return handleSetCourse(action, state);

    if (action.type == "connect")
        return handleConnect(action, state);

    if (action.type == "steer")
        return handleSteer(action, state);

    return ActionResult::invalid("Unknown envision action: " + action.type);
}

bool EnvisionPhaseHandler::tryParseInt(const std::string& s, int& value) 
{
    try{
        size_t pos = 0;
        int parsed = std::stoi(s, &pos);
        if (pos != s.size()){
            return false;
        }
        value = parsed;
        return true;
    }catch(...){
        return false;
    }
}

/** 
 * Shit Power 
 * - Cost: 1 HR
 * - Effect: First player token held by Player "targetPlayerId"
 */
ActionResult EnvisionPhaseHandler::handleShiftPower(const Action& action, GameState& state)
{
    auto it = action.params.find("targetPlayerId");
    if (it == action.params.end())
        return {ActionStatus::INVALID_ACTION, {"Envision", "Missing param: targetPlayerId"}};

    int targetPlayerId = -1;
    if (!tryParseInt(it->second, targetPlayerId))
        return {ActionStatus::INVALID_ACTION, {"Envision", "targetPlayerId must be an integer"}};

    Player* target = state.getPlayer(targetPlayerId);
    Player* currFirst;

    if (!target)
        return {ActionStatus::INVALID_TARGET,{"Envision","Target player does not exist"}};

    /* Cost: 1 HR */ 
    int hr_cost = 1;
    if (state.params.getParamAmount(CyberParameter::HUMAN_RELATION) < hr_cost)
        return {ActionStatus::INSUFFICIENT_RESOURCE, {"Envision", "Not enough People Relationship"}};

    /* Update First Player */
    currFirst = state.getPlayer(state.findFirstPlayer());
    if (currFirst) {

        currFirst->setFirstPlayer(false);
        target->setFirstPlayer(true);

        /* Apply action cost */
        state.params.adjustParam(CyberParameter::HUMAN_RELATION, -hr_cost);
    } else
        return {ActionStatus::INVALID_ACTION, {"Envision", "BUG_ON! Current first player is null"}};

    return ActionResult::success({"Envision","First-player token shifted to player" + std::to_string(targetPlayerId)});
}

/* Come Together Cost: 1 Env */
ActionResult EnvisionPhaseHandler::handleComeTogether(const Action& action, GameState& state)
{
    int ct_cost = 1;
    int curr_env = state.params.getParamAmount(CyberParameter::ENVIRONMENT);
    if (curr_env < ct_cost)
        return {ActionStatus::INVALID_ACTION,
                {"Envision", "Not enough environment relationship for come_together action"}};

    // Cost 1 environment, gain 1 cohesion
    state.params.adjustParam(CyberParameter::ENVIRONMENT, -ct_cost);
    state.params.adjustParam(CyberParameter::COHESION, 1);

    return {ActionResult::success({"Envision", "Spent " + std::to_string(ct_cost) + 
                                  " environment relationship to increase cohesion by 1"})};
}

/** 
 * Prepare: 
 * - Cost: 2 Hr
 * - Gain: 1 Cy
 */
ActionResult EnvisionPhaseHandler::handlePrepare(const Action& action, GameState& state)
{
    int totalCost = 2;
    int currentPeople = state.params.getParamAmount(CyberParameter::HUMAN_RELATION);
    if (currentPeople < totalCost)
        return ActionResult::invalid("Not enough people relationship for Prepare");

    // Cost 2 HR relationships, gain 1 cybernation level
    state.params.adjustParam(CyberParameter::HUMAN_RELATION, -totalCost);
    state.params.adjustParam(CyberParameter::CYBERNATION_LEVEL, 1);

    return {ActionResult::success({"Envision","Spent " + std::to_string(totalCost) +
                                   " people relationships to increase cybernation level by 1"})};
}

/**
 * Set course: move people token on tile, or rotate stack on a tile.
 */
ActionResult EnvisionPhaseHandler::handleSetCourse(const Action& action, GameState& state)
{
    int totalCost = 2;
    auto modeIt = action.params.find("mode");
    if (modeIt == action.params.end())
        return ActionResult::invalid("Missing param: mode");

    const std::string& mode = modeIt->second;

    // Cost check: Set Course costs 2 technologies
    if (state.params.getParamAmount(CyberParameter::TECHNOLOGY) < totalCost){
        return {ActionStatus::INSUFFICIENT_RESOURCE,
                {"Envision", "Not enough technology to use Set Course (requires 2)"}};
    }

    if (mode == "move_people") {
        auto tileIt = action.params.find("tile");
        auto sideIt = action.params.find("side");

        if (tileIt == action.params.end())
            return {ActionStatus::INVALID_ACTION, {"Envision", "Missing param: tile"}};

        if (sideIt == action.params.end())
            return {ActionStatus::INVALID_ACTION, {"Envision", "Missing param: side"}};

        int targetTile = -1;
        int targetSide = -1;

        if (!tryParseInt(tileIt->second, targetTile))
            return {ActionStatus::INVALID_ACTION, {"Envision", "tile must be an integer"}};

        if (!tryParseInt(sideIt->second, targetSide))
            return {ActionStatus::INVALID_ACTION, {"Envision", "side must be an integer"}};

        if (targetTile < 0 || targetTile >= static_cast<int>(state.board.size()))
            return {ActionStatus::INVALID_TARGET, {"Envision", "Target tile is our of range"}};

        if (targetSide < 0 || targetSide >= Tile::TILE_SIDES)
            return {ActionStatus::INVALID_TARGET, {"Envision", "Target side must be between 0 and 5"}};

        Tile* tile = state.getTile(targetTile);
        if (!tile)
            return {ActionStatus::INVALID_TARGET, {"Envision", "Target tile does not exist"}};

        // Map edge only: people stand on a side with no neighbor hex (see layout.json).
        if (tile->getNeighbourTile(targetSide) != -1) {
            return {ActionStatus::INVALID_TARGET,
                    {"Envision",
                     "People token can only be placed on an outer edge (hex side with no "
                     "neighbor). Tile " +
                         std::to_string(targetTile) + " side " + std::to_string(targetSide) +
                         " faces tile " + std::to_string(tile->getNeighbourTile(targetSide)) +
                         ", so choose another side or tile."}};
        }

        if (state.setPeopleToken({targetTile, targetSide})) {
            state.params.adjustParam(CyberParameter::TECHNOLOGY, -totalCost);
            return ActionResult::success({
                "Envision",
                "People token moved to tile " + std::to_string(targetTile) +
                ", side " + std::to_string(targetSide) +
                " (-2 techonology)"
            });
        }

        return {ActionStatus::INVALID_ACTION, {"Envision", "Failed to set people token"}};
    }

    if (mode == "rotate") {
        auto tileIt = action.params.find("tile");
        if (tileIt == action.params.end()) {
            return ActionResult::invalid("Missing param: tile");
        }
        int targetTile = -1;
        if (!tryParseInt(tileIt->second, targetTile)) {
            return ActionResult::invalid("tile must be an integer");
        }
        if (targetTile < 0 || targetTile >= static_cast<int>(state.board.size())) {
            return {ActionStatus::INVALID_TARGET, {"error", "Target tile is out of range"}};
        }

        Tile* tile = state.getTile(targetTile);
        if (tile == nullptr) {
            return {ActionStatus::INVALID_TARGET, {"error", "Target tile does not exist"}};
        }

        int steps = 1;
        auto stepsIt = action.params.find("steps");
        if (stepsIt == action.params.end()) {
            stepsIt = action.params.find("degree");
        }
        if (stepsIt != action.params.end()) {
            if (!tryParseInt(stepsIt->second, steps)) {
                return ActionResult::invalid("steps/degree must be an integer");
            }
        }

        if (steps < 0) {
            return ActionResult::invalid("steps/degree must be non-negative");
        }

        std::string direction = "clockwise";
        auto dirIt = action.params.find("direction");
        if (dirIt != action.params.end()) {
            direction = dirIt->second;
        }

        int currentRotation = tile->getRotation();
        int normalizedSteps = steps % Tile::TILE_SIDES;
        int newRotation = currentRotation;

        if (direction == "clockwise") {
            newRotation = (currentRotation + normalizedSteps) % Tile::TILE_SIDES;
        } else if (direction == "counterclockwise") {
            newRotation = (currentRotation - normalizedSteps + Tile::TILE_SIDES) % Tile::TILE_SIDES;
        } else {
            return ActionResult::invalid("direction must be clockwise or counterclockwise");
        }

        tile->setRotation(newRotation);
        state.params.adjustParam(CyberParameter::TECHNOLOGY, -totalCost);

        return ActionResult::success({
            "info",
            "Stack rotated on tile " + std::to_string(targetTile) +
            ", direction " + direction +
            ", steps " + std::to_string(normalizedSteps) +
            ", new rotation = " + std::to_string(newRotation)
        });
    }

    return ActionResult::invalid("Unsupported set_course mode");
}

/**
 * Connect： Trade 2 resources for 1 
 */
ActionResult EnvisionPhaseHandler::handleConnect(const Action& action, GameState& state)
{

    // We need 2 parameters from the front end: cost relationship type & gain relationship type
    if (action.params.find("cost") == action.params.end() ||
        action.params.find("gain") == action.params.end()) {
        return {ActionStatus::INVALID_ACTION, {"error", "Required cost and gain parameters"}};
    }

    CyberParameter cost, gain;
    if (!(parseCyberParameter(action.params.at("cost"), cost)))
        return {ActionStatus::INVALID_ACTION, {"error", "Invalid cost parameters"}};
    
    if (!(parseCyberParameter(action.params.at("gain"), gain)))
        return {ActionStatus::INVALID_ACTION, {"error", "Invalid gain parameters"}};

    auto isRelationship = [](CyberParameter p){
        return p == CyberParameter::HUMAN_RELATION ||
               p == CyberParameter::ENVIRONMENT ||
               p == CyberParameter::TECHNOLOGY;
    };

    if (!isRelationship(cost)|| !isRelationship(gain)){
        return{
            ActionStatus::INVALID_ACTION,
            {"error", "CONNECT only supports relationship parameters"}
        };
    }
 
    // check if it is enough to pay 2 relationships.
    if (state.params.getParamAmount(cost) < 2) {
        return {
            ActionStatus::INSUFFICIENT_RESOURCE,
            {"error", "Not enough cost for CONNECT (need 2)"}
        };
    }

    // reject no-op/same-resource exchange.
    if (cost == gain){
        return {
            ActionStatus::INVALID_ACTION,
            {"error", "Cost and gain parameters should be different"}
        };
    }

    // Avoid paying cost when gain is already capped
    if (state.params.getParamAmount(gain) >= state.params.getParamAmount(CyberParameter::COHESION)){
        return{
            ActionStatus::INVALID_ACTION,
            {"error", "Gain parameter is already at cohesion cap"}
        };       
    }

    state.params.adjustParam(cost, -2);
    state.params.adjustParam(gain, +1);

    return ActionResult::success({
        "info",
        "Spent 2 " + cyberParameterToLabel(cost) + " and gained 1 " + cyberParameterToLabel(gain)
    });
}

/**
 * Steer:
 * - Cost: 2 Env
 * - Gain: 
 *  - Put 1 feedback token into bags (from reserve)
 *  - Draw token from bags & choose whether discard or not  
 */
ActionResult EnvisionPhaseHandler::handleSteer(const Action& action, GameState& state)
{
    auto tokenIt = action.params.find("tokenType");
    if (tokenIt == action.params.end()) {
        return ActionResult::invalid("Missing param: tokenType");
    }

    TokenEffect effect = strToTokenEffect(tokenIt->second);
    if (effect == TokenEffect::UNKNOWN) {
        return ActionResult::invalid("Invalid tokenType");
    }

    if (!state.pool.draw(effect)) {
        return {
            ActionStatus::INSUFFICIENT_RESOURCE,
            {"Envision", "Selected feedback token is not available in reserve"}
        };
    }

    // Append to the live bag (rebuildTokenBag fills tokenBag but must stay synced with
    // tokenManager — never replace from an empty manager snapshot).
    std::vector<TokenEffect> bag = state.tokenBag;
    bag.push_back(effect);
    state.setTokenBag(bag);

    return ActionResult::success({
        "Envision",
        "Feedback token " + tokenEffectToStr(effect) + " added from reserve to bag"
    });
}
