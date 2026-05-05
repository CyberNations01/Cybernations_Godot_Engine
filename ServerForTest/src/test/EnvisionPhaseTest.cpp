#include "game/GameState.hpp"
#include "phase/EnvisionPhaseHandler.hpp"
#include "core/ActionResult.hpp"
#include <iostream>
#include <unordered_map>
#include <string>

void printResult(const std::string& testName, const ActionResult& res) {
    std::cout << "=== " << testName << " ===" << std::endl;
    std::cout << "ok: " << (res.ok() ? "true" : "false") << std::endl;
    std::cout << "type: " << res.message.type << std::endl;
    std::cout << "payload: " << res.message.payload << std::endl;
    std::cout << std::endl;
}

Action makeAction(
    int playerId,
    const std::string& type,
    const std::unordered_map<std::string, std::string>& params = {}
) {
    Action action;
    action.playerId = playerId;
    action.type = type;
    action.params = params;
    return action;
}

void printBasicParams(const GameState& state) {
    std::cout << "HR: " << state.params.getHumanRelation()
              << " | Env: " << state.params.getEnvironment()
              << " | Tech: " << state.params.getTechnology()
              << " | Cohesion: " << state.params.getCohesion()
              << " | Cybernation: " << state.params.getCybernationLevel()
              << std::endl;
}

void testDrawFeedbackTrackByFirstPlayer() {
    GameState state;
    EnvisionPhaseHandler handler;

    state.currentPhase = GamePhase::ENVISION;
    state.currentPlayerId = state.firstPlayerId;

    Action action = makeAction(state.firstPlayerId, "draw_feedback_track");
    ActionResult res = handler.handle(action, state);

    printResult("draw_feedback_track by first player", res);
}

void testDrawFeedbackTrackByNonFirstPlayer() {
    GameState state;
    EnvisionPhaseHandler handler;

    state.currentPhase = GamePhase::ENVISION;
    int nonFirst = (state.firstPlayerId + 1) % GameState::NUM_PLAYERS;
    state.currentPlayerId = nonFirst;

    Action action = makeAction(nonFirst, "draw_feedback_track");
    ActionResult res = handler.handle(action, state);

    printResult("draw_feedback_track by non-first player", res);
}

void testConnectMissingParams() {
    GameState state;
    EnvisionPhaseHandler handler;

    state.currentPhase = GamePhase::ENVISION;
    state.currentPlayerId = state.firstPlayerId;

    Action action = makeAction(state.currentPlayerId, "connect");
    ActionResult res = handler.handle(action, state);

    printResult("connect missing params", res);
}

void testConnectInvalidCostType() {
    GameState state;
    EnvisionPhaseHandler handler;

    state.currentPhase = GamePhase::ENVISION;
    state.currentPlayerId = state.firstPlayerId;

    Action action = makeAction(
        state.currentPlayerId,
        "connect",
        {
            {"costRelationshipType", "banana"},
            {"gainRelationshipType", "technology"}
        }
    );

    ActionResult res = handler.handle(action, state);
    printResult("connect invalid cost type", res);
}

void testConnectInvalidGainType() {
    GameState state;
    EnvisionPhaseHandler handler;

    state.currentPhase = GamePhase::ENVISION;
    state.currentPlayerId = state.firstPlayerId;

    Action action = makeAction(
        state.currentPlayerId,
        "connect",
        {
            {"costRelationshipType", "people"},
            {"gainRelationshipType", "banana"}
        }
    );

    ActionResult res = handler.handle(action, state);
    printResult("connect invalid gain type", res);
}

void testConnectInsufficientResource() {
    GameState state;
    EnvisionPhaseHandler handler;

    state.currentPhase = GamePhase::ENVISION;
    state.currentPlayerId = state.firstPlayerId;

    state.params.setCohesion(10);
    state.params.setHumanRelation(1);

    std::cout << "=== connect insufficient resource (before) ===" << std::endl;
    printBasicParams(state);

    Action action = makeAction(
        state.currentPlayerId,
        "connect",
        {
            {"costRelationshipType", "people"},
            {"gainRelationshipType", "technology"}
        }
    );

    ActionResult res = handler.handle(action, state);
    printResult("connect insufficient resource", res);

    std::cout << "=== after ===" << std::endl;
    printBasicParams(state);
    std::cout << std::endl;
}

void testConnectSuccess() {
    GameState state;
    EnvisionPhaseHandler handler;

    state.currentPhase = GamePhase::ENVISION;
    state.currentPlayerId = state.firstPlayerId;

    state.params.setCohesion(10);
    state.params.setHumanRelation(5);
    state.params.setTechnology(2);

    std::cout << "=== connect success (before) ===" << std::endl;
    printBasicParams(state);

    Action action = makeAction(
        state.currentPlayerId,
        "connect",
        {
            {"costRelationshipType", "people"},
            {"gainRelationshipType", "technology"}
        }
    );

    ActionResult res = handler.handle(action, state);
    printResult("connect success", res);

    std::cout << "=== after ===" << std::endl;
    printBasicParams(state);
    std::cout << std::endl;
}

void testShiftPowerSuccess() {
    GameState state;
    EnvisionPhaseHandler handler;

    state.currentPhase = GamePhase::ENVISION;
    state.currentPlayerId = state.firstPlayerId;

    state.params.setCohesion(10);
    state.params.setHumanRelation(5);

    int oldFirst = state.firstPlayerId;
    int target = (oldFirst + 1) % GameState::NUM_PLAYERS;

    std::cout << "=== shift_power success (before) ===" << std::endl;
    std::cout << "old first player: " << oldFirst << std::endl;
    printBasicParams(state);

    Action action = makeAction(
        state.currentPlayerId,
        "shift_power",
        {
            {"targetPlayerId", std::to_string(target)}
        }
    );

    ActionResult res = handler.handle(action, state);
    printResult("shift_power success", res);

    std::cout << "=== after ===" << std::endl;
    std::cout << "new first player: " << state.firstPlayerId << std::endl;
    std::cout << "target is first player? "
              << (state.getPlayer(target)->isFirstPlayer() ? "true" : "false")
              << std::endl;
    printBasicParams(state);
    std::cout << std::endl;
}

void testComeTogetherSuccess() {
    GameState state;
    EnvisionPhaseHandler handler;

    state.currentPhase = GamePhase::ENVISION;
    state.currentPlayerId = state.firstPlayerId;

    state.params.setCohesion(10);
    state.params.setEnvironment(4);

    std::cout << "=== come_together (before) ===" << std::endl;
    printBasicParams(state);

    Action action = makeAction(state.currentPlayerId, "come_together");
    ActionResult res = handler.handle(action, state);

    printResult("come_together success", res);

    std::cout << "=== after ===" << std::endl;
    printBasicParams(state);
    std::cout << std::endl;
}

void testPrepareSuccess() {
    GameState state;
    EnvisionPhaseHandler handler;

    state.currentPhase = GamePhase::ENVISION;
    state.currentPlayerId = state.firstPlayerId;

    state.params.setCohesion(10);
    state.params.setHumanRelation(5);

    std::cout << "=== prepare (before) ===" << std::endl;
    printBasicParams(state);

    Action action = makeAction(state.currentPlayerId, "prepare");
    ActionResult res = handler.handle(action, state);

    printResult("prepare success", res);

    std::cout << "=== after ===" << std::endl;
    printBasicParams(state);
    std::cout << std::endl;
}

int main() {
    testDrawFeedbackTrackByFirstPlayer();
    testDrawFeedbackTrackByNonFirstPlayer();

    testConnectMissingParams();
    testConnectInvalidCostType();
    testConnectInvalidGainType();
    testConnectInsufficientResource();
    testConnectSuccess();

    testShiftPowerSuccess();
    testComeTogetherSuccess();
    testPrepareSuccess();

    return 0;
}