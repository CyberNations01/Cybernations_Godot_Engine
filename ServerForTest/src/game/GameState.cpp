#include "game/GameState.hpp"
#include "core/Types.hpp"
#include <algorithm>
#include <random>
#include <map>

namespace {
    bool isPosMatch(int tilePos, const std::optional<std::string>& pos)
    {
        if (!pos.has_value() || pos->empty()) return true;
        if (*pos == "inner") return tilePos == 0;
        if (*pos == "middle") return tilePos >= 1 && tilePos <= 6;
        if (*pos == "outer") return tilePos >= 7 && tilePos <= 10;
        return false;
    }
}

void GameState::randomizeBoard()
{
    std::vector<CardManager<Stack>*> managers = {
        &wildStackManager, &wasteStackManager, 
        &devAStackManager, &devBStackManager
    };

    for (auto* m : managers)
        m->shuffle();

    std::random_device rd;
    std::mt19937 gen(rd());
    std::uniform_int_distribution<int> dist(0, 3);

    for (int i = 0; i < NUM_TILE; ++i) {
        int type = dist(gen);
        board[i].setBase(managers[type]->draw());
    }
}

GameState::GameState()
{
    for (int i = 0; i < NUM_PLAYERS; ++i)
        players[i] = Player(i);
    players[0].setFirstPlayer(true);

    DataLoader loader;
    disruptionManager = CardManager<DisruptionCard>(loader.loadDisrupt("./data/disruption.json"));
    board = loader.loadTile("./data/layout.json");
    goalManager = loader.loadDeck<Goal>("./data/goal.json");
    if (!goalManager.isDeckEmpty())
        currentGoal = goalManager.draw();

    CardManager<Stack> allStackManager = loader.loadDeck<Stack>("./data/stack.json");
    std::map<StackType, std::vector<Stack>> stacksByType;
    for (const auto& s : allStackManager.getDeck())
        stacksByType[s.getType()].push_back(s);

    wildStackManager  = CardManager<Stack>(stacksByType[StackType::WILD]);
    wasteStackManager = CardManager<Stack>(stacksByType[StackType::WASTE]);
    devAStackManager  = CardManager<Stack>(stacksByType[StackType::DEV_A]);
    devBStackManager  = CardManager<Stack>(stacksByType[StackType::DEV_B]);

    disruptionManager.shuffle();

    peopleToken = {4, 4};
    randomizeBoard();
    rebuildTokenBag();
    adaptTrack.clear();
    adaptCursor = 0;
}

Tile* GameState::getTile(int position)
{
    if (position < 0 || position >= NUM_TILE)
        return nullptr;
    return &board[position];
}

Player* GameState::getPlayer(int id)
{
    if (id < 0 || id >= NUM_PLAYERS) return nullptr;
    return &players[id];
}

bool GameState::setPeopleToken(const std::pair<int, int>& pos)
{
    int tile = pos.first, side = pos.second;
    if (tile < 0 || side < 0 || tile >= NUM_TILE || side >= Tile::TILE_SIDES)
        return false;
    
    Tile t = board[tile];
    // ! People Token must stand on the edge 
    if (t.getNeighbourTile(side) != -1)
        return false;
        
    peopleToken = pos;
    return true;
}

int GameState::findFirstPlayer() const
{
    for (int i = 0; i < NUM_PLAYERS; ++i) {
        if (players[i].isFirstPlayer()) return i;
    }
    return 0;
}

void GameState::rebuildTokenBag()
{
    // Add current-board feedback tokens to the existing bag; leftover bag tokens stay there
    // across rounds unless Adapt cleanup returns/removes them by rule.
    for (const auto& tile : board) {
        TokenEffect token = mapStackTypeToFeedbackToken(tile.getEffectiveType());
        if (token == TokenEffect::UNKNOWN){
            continue;
        }
        if (pool.draw(token)){
            tokenBag.push_back(token);
        }
        // If reserve is empty for that token, we silently skip for now.
        // Later you can change this to logging or error handling.
    }

    // Shuffle bag
    std::random_device rd;
    std::mt19937 gen(rd());
    std::shuffle(tokenBag.begin(), tokenBag.end(), gen);

    // Keep FeedbackTokenManager in sync — other code paths read tokenManager.getBag().
    tokenManager.setBag(tokenBag);
}

void GameState::syncTokenBagFromManager()
{
    tokenBag = tokenManager.getBag();
}

void GameState::setTokenBag(const std::vector<TokenEffect>& nextBag)
{
    tokenManager.setBag(nextBag);
    syncTokenBagFromManager();
}

bool GameState::isActiveGoalMet() const
{
    const auto& conditions = currentGoal.getConditions();
    if (conditions.empty()) return false;

    for (const auto& cond : conditions) {
        int lhs = 0;
        if (cond.type == "Wild") {
            for (const auto& tile : board) {
                if (tile.getEffectiveType() == StackType::WILD &&
                    isPosMatch(tile.getPosition(), cond.position)) {
                    ++lhs;
                }
            }
        } else if (cond.type == "Waste") {
            for (const auto& tile : board) {
                if (tile.getEffectiveType() == StackType::WASTE &&
                    isPosMatch(tile.getPosition(), cond.position)) {
                    ++lhs;
                }
            }
        } else if (cond.type == "DevA") {
            for (const auto& tile : board) {
                if (tile.getEffectiveType() == StackType::DEV_A &&
                    isPosMatch(tile.getPosition(), cond.position)) {
                    ++lhs;
                }
            }
        } else if (cond.type == "DevB") {
            for (const auto& tile : board) {
                if (tile.getEffectiveType() == StackType::DEV_B &&
                    isPosMatch(tile.getPosition(), cond.position)) {
                    ++lhs;
                }
            }
        } else if (cond.type == "Co" || cond.type == "Cohesion") {
            lhs = params.getCohesion();
        } else if (cond.type == "Cy" || cond.type == "Cybernation") {
            lhs = params.getCybernationLevel();
        } else if (cond.type == "HR" || cond.type == "HumanRelation") {
            lhs = params.getHumanRelation();
        } else if (cond.type == "Env" || cond.type == "Environment") {
            lhs = params.getEnvironment();
        } else if (cond.type == "Tech" || cond.type == "Technology") {
            lhs = params.getTechnology();
        } else {
            return false;
        }

        if (!compareWithOp(lhs, cond.op, cond.num)) return false;
    }
    return true;
}

nlohmann::json GameState::toJson() const
{
    nlohmann::json j;

    // Game progress
    j["ignoreCohesionLossThisRound"] = ignoreCohesionLossThisRound;
    j["activeGoal"] = {
        {"id", currentGoal.getId()},
        {"name", currentGoal.getName()},
        {"met", isActiveGoalMet()}
    };

    // Parameters
    j["params"] = {
        {"cohesion",         params.getCohesion()},
        {"cybernationLevel", params.getCybernationLevel()},
        {"humanRelation",    params.getHumanRelation()},
        {"environment",      params.getEnvironment()},
        {"technology",       params.getTechnology()}
    };

    // Board
    j["board"] = nlohmann::json::array();
    for (const auto& t : board) {
        const Stack& stack = t.hasOverlay() ? t.getOverlay() : t.getBase();
        nlohmann::json paths = nlohmann::json::object();
        for (const auto& [from, to] : stack.getPaths()) {
            paths[std::to_string(t.stackSideToBoardSide(from))] = t.stackSideToBoardSide(to);
        }
        nlohmann::json sides = nlohmann::json::array();
        for (int side = 0; side < Tile::TILE_SIDES; ++side) {
            sides.push_back(t.getSideResources(side));
        }
        j["board"].push_back({
            {"position", t.getPosition()},
            {"type",     stackTypeToStr(t.getEffectiveType())},
            {"rotation", t.getRotation()},
            {"paths",    paths},
            {"sides",    sides}
        });
    }

    // Pool
    j["pool"] = {
        {"turnWild",       pool.getTurnWild()},
        {"loseCohesion",   pool.getLoseCohesion()},
        {"turnWaste",      pool.getTurnWaste()},
        {"solveDisruption", pool.getSolveDisrupt()},
        {"develop",        pool.getDevelop()},
        {"transform",      pool.getTransform()},
        {"totalRemaining", pool.getPoolSize()}
    };
    
    // Token bag (counts only — additive JSON for clients; tokenBag vector unchanged)
    j["tokenBagCount"] = static_cast<int>(tokenBag.size());
    {
        std::map<TokenEffect, int> bagCounts;
        for (const auto& te : tokenBag) {
            bagCounts[te]++;
        }
        static const TokenEffect kBagBreakdownOrder[] = {
            TokenEffect::TURN_WILD,
            TokenEffect::LOSE_COHESION,
            TokenEffect::TURN_WASTE,
            TokenEffect::SOLVE_DISRUPTION,
            TokenEffect::DEVELOP_STACK,
            TokenEffect::TRANSFORM_STACK,
            TokenEffect::UNKNOWN
        };
        nlohmann::json bagBreakdown = nlohmann::json::object();
        for (TokenEffect te : kBagBreakdownOrder) {
            auto it = bagCounts.find(te);
            bagBreakdown[tokenEffectToStr(te)] = (it != bagCounts.end()) ? it->second : 0;
        }
        j["tokenBagBreakdown"] = bagBreakdown;
    }
    j["peopleToken"] = {peopleToken.first, peopleToken.second};
    j["peopleTokenBoardSide"] = peopleToken.second;
    if (peopleToken.first >= 0 && peopleToken.first < static_cast<int>(board.size())) {
        j["peopleTokenStackSide"] = board[peopleToken.first].boardSideToStackSide(peopleToken.second);
    } else {
        j["peopleTokenStackSide"] = peopleToken.second;
    }

    nlohmann::json adaptTrackJson = nlohmann::json::array();
    for (const auto& te : adaptTrack) {
        adaptTrackJson.push_back(tokenEffectToStr(te));
    }
    j["adapt"] = {
        {"trackSize", static_cast<int>(adaptTrack.size())},
        {"cursor", adaptCursor},
        {"complete", (!adaptTrack.empty() && adaptCursor >= static_cast<int>(adaptTrack.size()))},
        {"track", adaptTrackJson}
    };


    // Players
    j["players"] = nlohmann::json::array();
    for (int i = 0; i < NUM_PLAYERS; ++i) {
        j["players"].push_back({
            {"id",           players[i].getId()},
            {"isFirstPlayer", players[i].isFirstPlayer()}
        });
    }

    if (activeDisruption.has_value()) {
        j["activeDisruption"] = {
            {"name",        activeDisruption->getName()},
            {"category",    activeDisruption->getCategory()},
            {"cancellable", activeDisruption->isCancellable()}
        };
    } else {
        j["activeDisruption"] = nullptr;
    }

    return j;
}

std::string GameState::snapshot() const
{
    return toJson().dump(2);
}
