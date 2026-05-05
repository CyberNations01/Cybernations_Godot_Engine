#include "phase/AdoptPhaseHandler.hpp"
#include <string>
#include <array>
#include <unordered_map>
#include "nlohmann/json.hpp"
#include "game/GameUtility.hpp"

namespace {
std::string stackTypeToLabel(StackType type) {
    switch (type) {
        case StackType::WILD: return "WILD";
        case StackType::WASTE: return "WASTE";
        case StackType::DEV_A: return "DEV_A";
        case StackType::DEV_B: return "DEV_B";
        case StackType::UNKNOWN:
        default: return "UNKNOWN";
    }
}

std::string ringLabelForCursor(int cursor)
{
    if (cursor <= 0) return "INNER";
    if (cursor <= 6) return "MIDDLE";
    return "OUTER";
}

ActionResult fail(ActionStatus status, const std::string& reason)
{
    return {status, ActionMessage("error", reason)};
}

struct AdaptRuntimeState {
    std::vector<TokenEffect> trackSnapshot;
    std::vector<int> placedOn;
    std::vector<bool> cancelled;
    std::vector<bool> tileOccupied;
};

std::unordered_map<const GameState*, AdaptRuntimeState> gAdaptRuntimeByState;

void resetRuntimeFromTrack(AdaptRuntimeState& runtime, const std::vector<TokenEffect>& track) {
    runtime.trackSnapshot = track;
    runtime.placedOn.assign(track.size(), -1);
    runtime.cancelled.assign(track.size(), false);
    runtime.tileOccupied.assign(GameState::NUM_TILE, false);
}

AdaptRuntimeState& runtimeFor(GameState& state) {
    AdaptRuntimeState& runtime = gAdaptRuntimeByState[&state];
    if (runtime.trackSnapshot != state.adaptTrack) {
        resetRuntimeFromTrack(runtime, state.adaptTrack);
    } else if (state.adaptCursor == 0 && !state.adaptTrack.empty()) {
        // A new Adapt sequence may be injected by tests or setup logic.
        resetRuntimeFromTrack(runtime, state.adaptTrack);
    } else if (runtime.tileOccupied.size() != static_cast<size_t>(GameState::NUM_TILE)) {
        runtime.tileOccupied.assign(GameState::NUM_TILE, false);
    }
    return runtime;
}

const DisruptionCard* findDisruptionCardByName(const GameState& state, const std::string& name) {
    for (const auto& card : state.disruptionManager.getDeck()) {
        if (card.getName() == name) return &card;
    }
    for (const auto& card : state.disruptionManager.getDiscard()) {
        if (card.getName() == name) return &card;
    }
    if (state.activeDisruption.has_value() && state.activeDisruption->getName() == name) {
        return &(*state.activeDisruption);
    }
    return nullptr;
}

ActionResult applyDisruptionByNameRepeated(const Action& action, GameState& state) {
    auto nameIt = action.params.find("disruption_name");
    if (nameIt == action.params.end()) {
        return fail(ActionStatus::INVALID_ACTION, "resolve_disruption requires disruption_name for named repeat");
    }

    const DisruptionCard* card = findDisruptionCardByName(state, nameIt->second);
    if (!card) {
        return fail(ActionStatus::INVALID_TARGET, "Unknown disruption_name");
    }

    std::string decision = "cancel";
    auto decisionIt = action.params.find("decision");
    if (decisionIt != action.params.end()) {
        decision = decisionIt->second;
    }
    if (decision != "cancel" && decision != "apply") {
        return fail(ActionStatus::INVALID_ACTION, "decision must be 'cancel' or 'apply'");
    }

    int times = 1;
    auto timesIt = action.params.find("times");
    if (timesIt != action.params.end()) {
        try {
            times = std::stoi(timesIt->second);
        } catch (...) {
            return fail(ActionStatus::INVALID_ACTION, "times must be integer");
        }
    }
    if (times <= 0) {
        return fail(ActionStatus::INVALID_ACTION, "times must be > 0");
    }

    nlohmann::json details = nlohmann::json::array();
    int processed = 0;
    for (int i = 0; i < times; ++i) {
        state.activeDisruption = *card;

        Action routed = action;
        routed.type = "resolve_disruption";

        if (decision == "cancel") {
            routed.params["cancel"] = "1";
        } else {
            routed.params.erase("cancel");
        }

        ActionResult utilResult = GameUtility::applyDisruptionEffect(state, routed);
        if (!utilResult.ok()) {
            return fail(utilResult.status, utilResult.message.payload);
        }

        details.push_back({
            {"step", i + 1},
            {"type", utilResult.message.type},
            {"payload", utilResult.message.payload}
        });
        ++processed;
    }

    nlohmann::json payload = {
        {"disruptionName", card->getName()},
        {"decision", decision},
        {"times", times},
        {"processed", processed},
        {"cancelApplied", decision == "cancel"},
        {"details", details}
    };
    return ActionResult::success(ActionMessage("disruption_effect_cancelled", payload.dump()));
}

}

bool AdoptPhaseHandler::isTileOccupiedForAdaptPrompt(const GameState& state, int tilePos) {
    if (tilePos < 0 || tilePos >= GameState::NUM_TILE) return true;
    auto it = gAdaptRuntimeByState.find(&state);
    if (it == gAdaptRuntimeByState.end()) return false;
    const auto& occupied = it->second.tileOccupied;
    if (occupied.empty() || static_cast<int>(occupied.size()) <= tilePos) return false;
    return occupied[tilePos];
}

bool AdoptPhaseHandler::preparePhase(GameState& state) {
    if (!state.adaptTrack.empty()) {
        runtimeFor(state);
        return true;
    }

    // At the start of a round, the visible Adapt track is generated from the current board.
    // Existing bag tokens stay in the bag; add newly available board tokens from the finite pool.
    state.rebuildTokenBag();
    return ensureAdaptTrackInitialized(state);
}

ActionResult AdoptPhaseHandler::handle(const Action& action, GameState& state) {
    if (action.isPass()) {
        return fail(ActionStatus::INVALID_ACTION, "Pass is not allowed during Adopt phase");
    }

    if (action.type == "resolve_feedback") {
        return handleResolveFeedback(action, state);
    }
    // In controlled Adopt flow, disruption is drawn by SOLVE_DISRUPTION feedback (Agora) only.
    if (action.type == "draw_disruption") {
        return fail(ActionStatus::INVALID_ACTION,
                    "draw_disruption is not valid during Adopt; use resolve_feedback (SOLVE_DISRUPTION)");
    }
    if (action.type == "resolve_disruption") {
        if (action.params.find("disruption_name") != action.params.end()) {
            return applyDisruptionByNameRepeated(action, state);
        }
        return GameUtility::applyDisruptionEffect(state, action);
    }

    return fail(ActionStatus::INVALID_ACTION,
                "Action '" + action.type + "' is not valid during Adopt phase");
}

bool AdoptPhaseHandler::fillFeedbackTrackFromCurrentBoard(GameState& state) {
    /* Only build bag from stacks when empty. Otherwise steer / extras in tokenBag would be wiped
       when Adapt first draws the 11-slot track (e.g. testing resolve_feedback with extra Develop). */
    if (state.tokenBag.empty()) {
        state.rebuildTokenBag();
    }
    if (state.tokenBag.empty()) {
        return false;
    }
    state.setTokenBag(state.tokenBag);
    if (!state.tokenManager.drawTrackFromBag(GameState::FEEDBACK_TRACK_SIZE)) {
        return false;
    }
    state.adaptTrack = state.tokenManager.getTrack();
    state.adaptCursor = 0;
    state.syncTokenBagFromManager();
    return !state.adaptTrack.empty();
}

bool AdoptPhaseHandler::ensureAdaptTrackInitialized(GameState& state)
{
    if (!state.adaptTrack.empty()) {
        runtimeFor(state);
        return true;
    }
    if (!fillFeedbackTrackFromCurrentBoard(state)) {
        return false;
    }
    runtimeFor(state);
    return true;
}

bool AdoptPhaseHandler::isAdaptComplete(const GameState& state) const {
    return !state.adaptTrack.empty() &&
           state.adaptCursor >= static_cast<int>(state.adaptTrack.size());
}

bool AdoptPhaseHandler::isAdaptTileAvailable(GameState& state, int tilePos) const {
    if (tilePos < 0 || tilePos >= GameState::NUM_TILE) return false;
    AdaptRuntimeState& runtime = runtimeFor(state);
    if (runtime.tileOccupied.empty()) return true;
    return !runtime.tileOccupied[tilePos];
}

ActionResult AdoptPhaseHandler::handleResolveFeedback(const Action& action, GameState& state) {
    if (!ensureAdaptTrackInitialized(state)) {
        return fail(ActionStatus::UNKNOWN_ERROR, "Adapt track could not be initialized");
    }

    if (isAdaptComplete(state)) {
        return ActionResult::success(ActionMessage("status", "All Adapt feedback tokens were already resolved"));
    }

    auto tileIt = action.params.find("target_tile");
    auto decisionIt = action.params.find("decision");
    if (tileIt == action.params.end() || decisionIt == action.params.end()) {
        return fail(ActionStatus::INVALID_ACTION, "resolve_feedback requires params: target_tile, decision");
    }

    int targetTile = -1;
    try {
        targetTile = std::stoi(tileIt->second);
    } catch (...) {
        return fail(ActionStatus::INVALID_TARGET, "target_tile must be an integer");
    }

    if (!isValidTargetForCursor(state.adaptCursor, targetTile)) {
        return fail(ActionStatus::INVALID_TARGET, "target_tile not valid for current Adapt slot");
    }

    if (!isAdaptTileAvailable(state, targetTile)) {
        return fail(ActionStatus::INVALID_TARGET, "target_tile already occupied this Adapt phase");
    }

    const std::string decision = decisionIt->second;
    const TokenEffect effect = state.adaptTrack[state.adaptCursor];
    AdaptRuntimeState& runtime = runtimeFor(state);

    nlohmann::json appliedEffect = nullptr;
    if (decision == "cancel") {
        if (state.params.getCybernationLevel() < 1) {
            return fail(ActionStatus::INSUFFICIENT_RESOURCE, "Not enough cybernation to cancel feedback");
        }
        state.params.adjustParam(CyberParameter::CYBERNATION_LEVEL, -1);
        runtime.cancelled[state.adaptCursor] = true;
    } else if (decision == "allow") {
        ActionResult effectResult = applyFeedbackEffect(effect, targetTile, action, state);
        if (!effectResult.ok()) return effectResult;
        runtime.cancelled[state.adaptCursor] = false;
        if (effectResult.message.type == "adapt_effect_applied") {
            try {
                appliedEffect = nlohmann::json::parse(effectResult.message.payload);
            } catch (...) {
                appliedEffect = effectResult.message.payload;
            }
        }
    } else {
        return fail(ActionStatus::INVALID_ACTION, "decision must be 'cancel' or 'allow'");
    }

    const int resolvedIndex = state.adaptCursor;
    runtime.placedOn[state.adaptCursor] = targetTile;
    runtime.tileOccupied[targetTile] = true;
    state.adaptCursor++;

    nlohmann::json payload = {
        {"resolvedIndex", resolvedIndex},
        {"token", tokenEffectToStr(effect)},
        {"targetTile", targetTile},
        {"decision", decision},
        {"effectApplied", appliedEffect},
        {"ring", ringLabelForCursor(resolvedIndex)},
        {"remaining", static_cast<int>(state.adaptTrack.size()) - state.adaptCursor},
        {"isComplete", isAdaptComplete(state)},
        {"cybernationLevel", state.params.getCybernationLevel()},
        {"cohesion", state.params.getCohesion()}
    };
    if (isAdaptComplete(state)) {
        payload["adaptPhaseCleanup"] = finalizeAdaptPhaseCleanup(state);
    }
    return ActionResult::success(ActionMessage("adapt_feedback_resolved", payload.dump()));
}

nlohmann::json AdoptPhaseHandler::finalizeAdaptPhaseCleanup(GameState& state) {
    AdaptRuntimeState& runtime = runtimeFor(state);

    // Rulebook step 2: return face-up on inner + four outer to bag; others to reserve.
    std::array<bool, GameState::NUM_TILE> returnable{};
    returnable.fill(false);
    returnable[0] = true;
    for (int t = 7; t <= 10; ++t) returnable[t] = true;

    std::vector<TokenEffect> nextBag = state.tokenBag;
    int returnedToBag = 0;
    int discarded = 0;
    for (int i = 0; i < static_cast<int>(state.adaptTrack.size()); ++i) {
        const int tilePos = (i < static_cast<int>(runtime.placedOn.size())) ? runtime.placedOn[i] : -1;
        const bool cancelled = (i < static_cast<int>(runtime.cancelled.size())) ? runtime.cancelled[i] : true;
        if (tilePos < 0 || tilePos >= GameState::NUM_TILE) continue;

        if (!cancelled && returnable[tilePos]) {
            nextBag.push_back(state.adaptTrack[i]);
            ++returnedToBag;
        } else {
            state.pool.putBack(state.adaptTrack[i]);
            ++discarded;
        }
    }
    state.setTokenBag(nextBag);
    state.adaptTrack.clear();
    state.adaptCursor = 0;
    resetRuntimeFromTrack(runtime, std::vector<TokenEffect>{});

    // Victory / turn-5 and phase advance are owned by RoundController (TBD).
    return {
        {"adaptTrackFinalized", true},
        {"returnedToBag", returnedToBag},
        {"discardedToReserve", discarded},
        {"bagSize", static_cast<int>(state.tokenBag.size())}
    };
}

bool AdoptPhaseHandler::isValidTargetForCursor(int cursor, int tilePos) const {
    if (cursor < 0 || cursor >= GameState::NUM_TILE) return false;
    if (tilePos < 0 || tilePos >= GameState::NUM_TILE) return false;

    // Slot 0 -> inner tile only.
    if (cursor == 0) return tilePos == 0;
    // Slots 1..6 -> middle ring (1..6).
    if (cursor >= 1 && cursor <= 6) return tilePos >= 1 && tilePos <= 6;
    // Slots 7..10 -> outer ring (7..10).
    return tilePos >= 7 && tilePos <= 10;
}

ActionResult AdoptPhaseHandler::applyFeedbackEffect(TokenEffect effect, int tilePos, const Action& action, GameState& state) {
    Tile* tile = state.getTile(tilePos);
    if (!tile) {
        return fail(ActionStatus::INVALID_TARGET, "Invalid target tile");
    }

    switch (effect) {
        case TokenEffect::TURN_WILD: {
            GameUtility::changeTileStack(state, tilePos, StackType::WILD);
            return ActionResult::success(ActionMessage("adapt_effect_applied", R"({"effect":"TURN_WILD"})"));
        }
        case TokenEffect::TURN_WASTE: {
            GameUtility::changeTileStack(state, tilePos, StackType::WASTE);
            return ActionResult::success(ActionMessage("adapt_effect_applied", R"({"effect":"TURN_WASTE"})"));
        }
        case TokenEffect::LOSE_COHESION:
            if (state.ignoreCohesionLossThisRound) {
                return ActionResult::success(ActionMessage("adapt_effect_applied", R"({"effect":"LOSE_COHESION","delta":0,"blocked":true})"));
            }
            state.params.adjustParam(CyberParameter::COHESION, -2);
            return ActionResult::success(ActionMessage("adapt_effect_applied", R"({"effect":"LOSE_COHESION","delta":-2})"));
        case TokenEffect::SOLVE_DISRUPTION:
            {
                ActionResult drawResult = GameUtility::drawDisruption(state);
                if (!drawResult.ok()) {
                    return fail(drawResult.status, drawResult.message.payload);
                }
                nlohmann::json payload = {
                    {"effect", "SOLVE_DISRUPTION"},
                    {"drawn", true},
                    {"disruptionName", state.activeDisruption.has_value() ? state.activeDisruption->getName() : "UNKNOWN"},
                    {"cancellable", state.activeDisruption.has_value() ? state.activeDisruption->isCancellable() : false}
                };
                return ActionResult::success(ActionMessage("adapt_effect_applied", payload.dump()));
            }
        case TokenEffect::DEVELOP_STACK:
            // If already developed, no effect.
            // If not, create DEV_A/DEV_B based on optional param:
            // develop_to: DEV_A|DEV_B|WORKS|AGORA (default DEV_A)
            if (!tile->hasOverlay()) {
                StackType targetType = StackType::DEV_A;
                auto it = action.params.find("develop_to");
                if (it != action.params.end()) {
                    const std::string& v = it->second;
                    if (v == "DEV_B" || v == "AGORA") {
                        targetType = StackType::DEV_B;
                    }
                }
                GameUtility::changeTileStack(state, tilePos, targetType);
            }
            {
                nlohmann::json payload = {
                    {"effect", "DEVELOP_STACK"},
                    {"overlayType", tile->hasOverlay() ? stackTypeToLabel(tile->getOverlay().getType()) : "NONE"}
                };
                return ActionResult::success(ActionMessage("adapt_effect_applied", payload.dump()));
            }
        case TokenEffect::TRANSFORM_STACK:
            // Toggle Works/Agora (DevA <-> DevB) using effective stack type — development may live on
            // overlay only, or (e.g. after randomizeBoard) as base stack with no overlay.
            {
                const StackType eff = tile->getEffectiveType();
                if (eff == StackType::DEV_A) {
                    GameUtility::changeTileStack(state, tilePos, StackType::DEV_B);
                } else if (eff == StackType::DEV_B) {
                    GameUtility::changeTileStack(state, tilePos, StackType::DEV_A);
                }
            }
            return ActionResult::success(ActionMessage("adapt_effect_applied", R"({"effect":"TRANSFORM_STACK"})"));
        case TokenEffect::UNKNOWN:
        default:
            return ActionResult::success(ActionMessage("adapt_effect_applied", R"({"effect":"UNKNOWN","ignored":true})"));
    }
}
