#ifndef TRAVERSE_PHASE_HANDLER_HPP
#define TRAVERSE_PHASE_HANDLER_HPP

#include "game/PhaseHandler.hpp"
#include "game/GameUtility.hpp"

/*
 * TraversePhaseHandler
 * 
 * Valid actions during Traverse phase:
 * 
 * - `walkPath`: Walk People token and claim resource
 * - `Solve Disruption`: Draw Disruption and handle the effect
 */

class TraversePhaseHandler : public PhaseHandler {
public:
    ActionResult handle(const Action& action, GameState& state) override;
    GamePhase    getPhase() const override { return GamePhase::TRAVERSE; }

private:
    ActionResult handleWalkPath(GameState &state);
    ActionResult handleDrawDisruption(GameState& state);
    ActionResult handleResolveDisruption(const Action& action, GameState& state);

};

#endif
