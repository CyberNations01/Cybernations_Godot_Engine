#ifndef ENVISION_PHASE_HANDLER_HPP
#define ENVISION_PHASE_HANDLER_HPP

#include "game/PhaseHandler.hpp"
#include "game/GameState.hpp"

/*
 * EnvisionPhaseHandler
 * 
 * Current minimal implementation supports:
 * 
 *   - "shift_power" : pass the first-player token to another player 
 *   - "come_together": increase cohesion by 1
 *   - "prepare": increase cybernation level by 1
 *   - "set_course": move people token to a target tile/side. (simple)
 * 
 * 
 * Reserved for future extension:
 *   - "connect"
 *   - "steer"
 */

class EnvisionPhaseHandler : public PhaseHandler {
public:
    ActionResult handle(const Action& action, GameState& state) override;
    GamePhase    getPhase() const override { return GamePhase::ENVISION; }

private:
    ActionResult handleShiftPower(const Action& action, GameState& state);
    ActionResult handleComeTogether(const Action& action, GameState& state);
    ActionResult handlePrepare(const Action& action, GameState& state);
    ActionResult handleSetCourse(const Action& action, GameState& state);
    
    ActionResult handleConnect(const Action& action, GameState& state);
    ActionResult handleSteer(const Action& action, GameState& state);

private:
    bool tryParseInt(const std::string& s, int& value);
};

#endif
