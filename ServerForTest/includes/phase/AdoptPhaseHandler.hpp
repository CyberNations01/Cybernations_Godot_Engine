#ifndef ADOPT_PHASE_HANDLER_HPP
#define ADOPT_PHASE_HANDLER_HPP

#include "game/PhaseHandler.hpp"
#include "nlohmann/json.hpp"

/*
 * AdoptPhaseHandler
 * 
 * Adapt-phase responsibilities:
 *   - Resolve feedback tokens in strict slot order (inner -> middle -> outer)
 *   - Allow cancel/allow decisions with cybernation payment
 *   - Apply token effects (including DevA/DevB development transitions)
 *   - During Adopt, disruption draw is triggered only by SOLVE_DISRUPTION feedback;
 *     explicit draw_disruption action is not allowed (resolve_disruption remains available)
 *   - Expose Adapt status for UI synchronization
 *   - When the last feedback token is resolved, step-2 token bag / reserve cleanup runs automatically
 *     (victory / turn end will be handled by RoundController)
 * 
 * This phase assumes first-player order has already been determined
 * by previous phases; it does not reassign first player.
 */

class AdoptPhaseHandler : public PhaseHandler {
public:
    ActionResult handle(const Action& action, GameState& state) override;
    GamePhase    getPhase() const override { return GamePhase::ADOPT; }
    static bool isTileOccupiedForAdaptPrompt(const GameState& state, int tilePos);
    bool preparePhase(GameState& state);

private:
    ActionResult handleResolveFeedback(const Action& action, GameState& state);
    nlohmann::json finalizeAdaptPhaseCleanup(GameState& state);

    bool ensureAdaptTrackInitialized(GameState& state);
    /// When Adapt needs a track and adaptTrack is empty: if tokenBag is empty, rebuild from board;
    /// otherwise keep existing bag (preserves steer extras). Then shuffle & fill adaptTrack from bag.
    static bool fillFeedbackTrackFromCurrentBoard(GameState& state);
    bool isAdaptComplete(const GameState& state) const;
    bool isAdaptTileAvailable(GameState& state, int tilePos) const;

    bool isValidTargetForCursor(int cursor, int tilePos) const;
    ActionResult applyFeedbackEffect(TokenEffect effect, int tilePos, const Action& action, GameState& state);
};

#endif
