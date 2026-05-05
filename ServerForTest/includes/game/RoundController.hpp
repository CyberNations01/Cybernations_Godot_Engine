#ifndef ROUND_CONTROLLER_HPP
#define ROUND_CONTROLLER_HPP

#include "game/GameState.hpp"
#include "phase/AdoptPhaseHandler.hpp"
#include "phase/EnvisionPhaseHandler.hpp"
#include "phase/TraversePhaseHandler.hpp"
#include "core/Action.hpp"
#include "core/ActionResult.hpp"
#include <set>


class RoundController {
public:

    RoundController(GameState& state);
    ActionResult processAction(const Action& action, GameState& state);

    int            getCurrentPlayerId() const { return currentPlayerId; };
    GamePhase      getCurrentPhase()    const { return currentPhase; };
    int            getCurrentRound()    const { return currentRound; };
    bool           isGameOver()         const { return gameOver; };
    nlohmann::json toJson()   const;
    std::string    snapshot() const;

private:

    int       currentRound    = 1;
    int       maxRounds       = 5;
    GamePhase currentPhase    = GamePhase::ENVISION;
    bool      gameOver        = false;

    int firstPlayerId = 0;
    int nextFirstPlayerId = 0;

    int currentPlayerId = 0;
    struct envision_record {
        int idx = 0;
        int turn = 0;
        int maxTurn = 5;
    } envision_record;
    struct traverse_record {
        // 0: draw_disruption, 1: resolve_disruption, 2: walk_path
        int stage = 0;
    } traverse_record;
    struct adopt_record {
        bool pendingDisruptionResolution = false;
        bool completeAfterPendingResolution = false;
        int pendingPlayerId = -1;
    } adopt_record;
    
    std::array<int, GameState::NUM_PLAYERS> turnOrder;
    std::set<int> passedPlayers;

    EnvisionPhaseHandler envisionHandler;
    TraversePhaseHandler traverseHandler;
    AdoptPhaseHandler    adoptHandler;
    
    PhaseHandler* getHandlerForPhase(GamePhase phase);
    bool handleEnvision(GameState& state);
    void syncFirstPlayerFromState(const GameState& state);

    void buildTurnOrder(int firstPlayerId);
    void resetRound(const GamePhase& nextPhase);
    bool advancePhase(GameState& state);
    bool advanceRound(GameState& state);
    bool advanceToNextActivePlayer();
};

#endif
