#ifndef PHASE_HANDLER_HPP
#define PHASE_HANDLER_HPP

#include "core/Action.hpp"
#include "core/ActionResult.hpp"
#include "game/GameState.hpp"

/*
 * PhaseHandler (Layer 2) — Game Logic Executor
 * 
 * Each phase (Envision, Traverse, Adopt) has a concrete handler.
 * 
 * Responsibilities:
 *   - Validate whether an action is legal in THIS phase
 *   - Mutate GameState directly when executing an action
 *   - Return ActionResult describing what happened
 * 
 * NOT responsible for:
 *   - Checking whose turn it is (RoundController does that)
 *   - Advancing phases or rounds (RoundController does that)
 *   - Checking if player has already passed (RoundController does that)
 */

class PhaseHandler {
public:
    virtual ~PhaseHandler() = default;

    /*
     * Execute an action on the game state.
     * 
     * Pre-condition: RoundController has already verified:
     *   - It IS this player's turn
     *   - Player has NOT already passed
     *   - The game is NOT over
     * 
     * The handler only needs to validate game-rule legality.
     */
    virtual ActionResult handle(const Action& action, GameState& state) = 0;

    /*
     * Return the phase this handler is responsible for.
     */
    virtual GamePhase getPhase() const = 0;
};

#endif
