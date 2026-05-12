#include "game/RoundController.hpp"
#include "phase/EnvisionPhaseHandler.hpp"
#include "phase/TraversePhaseHandler.hpp"
#include "phase/AdoptPhaseHandler.hpp"
#include "nlohmann/json.hpp"

RoundController::RoundController(GameState& state)
{
    syncFirstPlayerFromState(state);
    buildTurnOrder(firstPlayerId);
    adoptHandler.preparePhase(state);
}

ActionResult RoundController::processAction(const Action& action, GameState& state)
{
    if (action.playerId != currentPlayerId)
        return ActionResult::ignored();

    const auto& type = action.type;

    if (type == "pass") {
        if (currentPhase != GamePhase::ENVISION)
            return {ActionStatus::INVALID_ACTION, {"RoundController", "Current phase cannot be passed"}};
        
        int id = action.playerId;
        if (passedPlayers.find(id) == passedPlayers.end()) {

            passedPlayers.insert(id);
            if (advanceToNextActivePlayer()) {
                return ActionResult::success({"RoundController",
                                              "Player " + std::to_string(id) + " passed." +
                                              "Next player is " + std::to_string(currentPlayerId)});
            }
                
            else {
                advancePhase(state);
                return ActionResult::success({"RoundController",
                                              "Player " + std::to_string(id) + " passed." +
                                              "All players have passed"});
            }

        } else
            return ActionResult::ignored();
    }

    if (type == "advance") {
        if (currentPhase == GamePhase::TRAVERSE)
            return {ActionStatus::INVALID_ACTION, {"RoundController", "Traverse advances automatically after walk_path"}};
        if (currentPhase == GamePhase::ADOPT)
            return {ActionStatus::INVALID_ACTION, {"RoundController", "Adopt advances automatically after feedback sequence"}};
        if (!advancePhase(state))
            return {ActionStatus::INVALID_ACTION, {"RoundController", "Phase is not advanced"}};
        
        if (gameOver) {
            std::string reason = state.isActiveGoalMet() ? "Goal met!" : "Out of rounds";
            return ActionResult::success({"RoundController", "Game Over: " + reason});
        }

        return ActionResult::success({"RoundController", 
            "Phase advanced to " + gamePhaseToStr(currentPhase)});
    }

    PhaseHandler* handler = getHandlerForPhase(currentPhase);
    if (!handler)
        return {ActionStatus::INVALID_ACTION, {"BUG", "No handler for current phase"}};
    
    if (currentPhase == GamePhase::ENVISION) {
        if (!handleEnvision(state))
            return {ActionStatus::INVALID_ACTION, {"RoundController",
                                                   "Maximum turn for Envision phase is reached."
                                                   "Rest of the players must passed."}};
    }
    else if (currentPhase == GamePhase::TRAVERSE) {
        const char* required = (traverse_record.stage == 0) ? "draw_disruption"
                              : (traverse_record.stage == 1) ? "resolve_disruption"
                                                             : "walk_path";
        if (type != required) {
            return {ActionStatus::INVALID_ACTION, {"RoundController",
                    "Traverse action order is enforced: draw_disruption -> resolve_disruption -> walk_path"}};
        }
    }
    else if (currentPhase == GamePhase::ADOPT) {
        if (type == "draw_disruption") {
            return {ActionStatus::INVALID_ACTION, {"RoundController",
                    "draw_disruption is not allowed in controlled Adopt flow; use resolve_feedback"}};
        }
        if (type == "resolve_feedback" && adopt_record.pendingDisruptionResolution) {
            return {ActionStatus::INVALID_ACTION, {"RoundController",
                    "Current player must resolve_disruption for Agora before next resolve_feedback"}};
        }
        if (type == "resolve_disruption" && !adopt_record.pendingDisruptionResolution) {
            return {ActionStatus::INVALID_ACTION, {"RoundController",
                    "resolve_disruption is only allowed when Agora feedback drew a disruption"}};
        }
        if (type == "resolve_disruption" &&
            adopt_record.pendingPlayerId >= 0 &&
            action.playerId != adopt_record.pendingPlayerId) {
            return {ActionStatus::INVALID_ACTION, {"RoundController",
                    "Agora disruption must be resolved by the same player"}};
        }
    }

    ActionResult result = handler->handle(action, state);
    if (!result.ok())
        return result;

    // In Envision, a successful action should hand turn to the next active player.
    if (currentPhase == GamePhase::ENVISION)
        advanceToNextActivePlayer();
    else if (currentPhase == GamePhase::TRAVERSE) {
        if (type == "draw_disruption") {
            traverse_record.stage = 1;
        } else if (type == "resolve_disruption") {
            traverse_record.stage = 2;
        } else if (type == "walk_path") {
            traverse_record.stage = 0;
            advancePhase(state);
        }
    }
    else if (currentPhase == GamePhase::ADOPT) {
        if (type == "resolve_feedback") {
            bool isComplete = false;
            bool needsAgoraResolve = false;
            auto payload = nlohmann::json::parse(result.message.payload, nullptr, false);
            if (!payload.is_discarded()) {
                isComplete = payload.value("isComplete", false);
                const std::string token = payload.value("token", "");
                const std::string decision = payload.value("decision", "");
                needsAgoraResolve = (token == "SOLVE_DISRUPTION" &&
                                     decision == "allow" &&
                                     state.activeDisruption.has_value());
            }

            if (needsAgoraResolve) {
                adopt_record.pendingDisruptionResolution = true;
                adopt_record.completeAfterPendingResolution = isComplete;
                adopt_record.pendingPlayerId = action.playerId;
            } else if (isComplete) {
                advancePhase(state);
            } else {
                advanceToNextActivePlayer();
            }
        } else if (type == "resolve_disruption") {
            adopt_record.pendingDisruptionResolution = false;
            if (adopt_record.completeAfterPendingResolution) {
                adopt_record.completeAfterPendingResolution = false;
                adopt_record.pendingPlayerId = -1;
                advancePhase(state);
            } else {
                adopt_record.completeAfterPendingResolution = false;
                adopt_record.pendingPlayerId = -1;
                advanceToNextActivePlayer();
            }
        }
    }

    return result;
}

PhaseHandler* RoundController::getHandlerForPhase(GamePhase phase)
{
    switch (phase) {
        case GamePhase::ENVISION: return &envisionHandler;
        case GamePhase::TRAVERSE: return &traverseHandler;
        case GamePhase::ADOPT:    return &adoptHandler;
    }
    return nullptr;
}

void RoundController::buildTurnOrder(int firstPlayerId)
{
    int id = firstPlayerId;

    for (size_t i = 0; i < turnOrder.size(); i++, id++)
        turnOrder[i] = id % GameState::NUM_PLAYERS;
    
    currentPlayerId = firstPlayerId;
}

void RoundController::resetRound(const GamePhase& nextPhase)
{
    buildTurnOrder(firstPlayerId);
    passedPlayers.clear();

    if (nextPhase == GamePhase::ENVISION) {
        envision_record.idx = 0;
        envision_record.turn = 0;
    }
}

bool RoundController::handleEnvision(GameState& state)
{
    if (envision_record.turn >= envision_record.maxTurn)
        return false;

    // Action Cost
    switch (envision_record.turn) {
        case 3:
        case 4:
            state.params.adjustParam(CyberParameter::TECHNOLOGY, -1);
            break;
        case 5:
            state.params.adjustParam(CyberParameter::TECHNOLOGY, -2);
            break;
        default:
            break;
    }

    int activePlayers = GameState::NUM_PLAYERS - static_cast<int>(passedPlayers.size());
    ++envision_record.idx;
    if (envision_record.idx >= activePlayers) {
        envision_record.idx = 0;
        ++envision_record.turn;
    }
    return true;
}

bool RoundController::advancePhase(GameState& state)
{
    switch (currentPhase) {
        case GamePhase::ENVISION:
            if (advanceToNextActivePlayer())
                return false;
            currentPhase = GamePhase::TRAVERSE;
            traverse_record.stage = 0;
            passedPlayers.clear();
            break;

        case GamePhase::TRAVERSE:
            if (!adoptHandler.preparePhase(state))
                return false;
            currentPhase = GamePhase::ADOPT;
            traverse_record.stage = 0;
            adopt_record.pendingDisruptionResolution = false;
            adopt_record.completeAfterPendingResolution = false;
            adopt_record.pendingPlayerId = -1;
            passedPlayers.clear();
            break;
        
        case GamePhase::ADOPT:
            advanceRound(state);
            if (gameOver)
                return true;
            currentPhase = GamePhase::ENVISION;
            if (!adoptHandler.preparePhase(state))
                return false;
            break;
    }

    syncFirstPlayerFromState(state);
    buildTurnOrder(firstPlayerId);
    return true;
}

void RoundController::syncFirstPlayerFromState(const GameState& state)
{
    int playerId = -1;
    for (const auto& p : state.players) {
        if (p.isFirstPlayer()) {
            playerId = p.getId();
            break;
        }
    }
    firstPlayerId = (playerId < 0) ? 0 : playerId;
}

bool RoundController::advanceRound(GameState& state)
{
    if (state.isActiveGoalMet()) {
        gameOver = true;
        return true;
    }

    if (++currentRound == maxRounds) {
        gameOver = true;
        return false;
    }

    resetRound(GamePhase::ENVISION);
    return true;
}

bool RoundController::advanceToNextActivePlayer()
{
    if (passedPlayers.size() == GameState::NUM_PLAYERS)
        return false;

    int nextPlayerId = (currentPlayerId + 1) % GameState::NUM_PLAYERS;
    for (int i = 0; i < GameState::NUM_PLAYERS; i++) {
        if (passedPlayers.find(nextPlayerId) == passedPlayers.end()) {
            currentPlayerId = nextPlayerId;
            return true;
        }
        nextPlayerId = (nextPlayerId + 1) % GameState::NUM_PLAYERS;
    }
    return false;
}

nlohmann::json RoundController::toJson() const 
{
    nlohmann::json j;
    nlohmann::json allowedActions = nlohmann::json::array();
    std::string recommendedAction;

    switch (currentPhase) {
        case GamePhase::ENVISION:
            allowedActions = {"shift_power", "come_together", "prepare", "set_course",
                              "connect", "steer", "pass", "advance"};
            recommendedAction = "pass";
            break;
        case GamePhase::TRAVERSE:
            if (traverse_record.stage == 0) {
                allowedActions = {"draw_disruption"};
                recommendedAction = "draw_disruption";
            } else if (traverse_record.stage == 1) {
                allowedActions = {"resolve_disruption"};
                recommendedAction = "resolve_disruption";
            } else {
                allowedActions = {"walk_path"};
                recommendedAction = "walk_path";
            }
            break;
        case GamePhase::ADOPT:
            if (adopt_record.pendingDisruptionResolution) {
                allowedActions = {"resolve_disruption"};
                recommendedAction = "resolve_disruption";
            } else {
                allowedActions = {"resolve_feedback"};
                recommendedAction = "resolve_feedback";
            }
            break;
    }

    j["round"] = currentRound;
    j["phase"] = gamePhaseToStr(currentPhase);
    j["gameOver"] = gameOver;
    j["next_player_id"] = currentPlayerId;
    j["passedPlayers"] = nlohmann::json::array();
    for (const auto& playerId : passedPlayers) {
        j["passedPlayers"].push_back(playerId);
    }
    j["allowed_actions"] = allowedActions;
    j["recommended_action"] = recommendedAction;
    j["traverse_stage"] = traverse_record.stage;
    j["adopt_pending_disruption_resolution"] = adopt_record.pendingDisruptionResolution;
    j["adopt_pending_player_id"] = adopt_record.pendingPlayerId;
    return j;
}

std::string RoundController::snapshot() const
{
    return toJson().dump(2);
}
