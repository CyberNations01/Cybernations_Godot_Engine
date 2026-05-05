#ifndef GAME_UTILITY_HPP
#define GAME_UTILITY_HPP
#include "core/Types.hpp"
#include "core/ActionResult.hpp"
#include "core/Action.hpp"
#include "core/DisruptionCard.hpp"
#include "game/GameState.hpp"

class GameUtility {
    public:
        static ActionResult walkPath(GameState& state);    
        static ActionResult drawDisruption(GameState& state);
        static ActionResult applyDisruptionEffect(GameState& state, const Action& action);
        static ActionResult tradeForDisruption(GameState& state, const Action& action);
        static void changeTileStack(GameState& state, int tilePos, StackType targetType);

    private:
        // ! WalkPath Internal
        static nlohmann::json pathResultToJson(
                    const int& tile, const int& side, 
                    const std::vector<std::string> & resources, 
                    const std::string& layer);

        // ! Disruption Card Internal
        static bool checkResourceCondition(GameState& state, ResourceCondition cond);
        static void resolveParamEffect(GameState & state, const std::pair<DisruptionEffect, int>& effect);
        static std::optional<ActionResult> cancelCard(GameState &state, const Action & action, const DisruptionCard &card);
        static std::vector<int> filterTilesByStackCondition(GameState &state,
                                                            const std::vector<int>& tiles,
                                                            const DisruptionCard& card);
        static std::optional<ActionResult> cancelOnTiles(GameState & state,
                                                         const DisruptionCard& card,
                                                         const Action &action,
                                                         std::vector<int>& effectiveStackTarget);
        /** Per-tile disruption effect: Turn* uses changeTileStack; numeric effects use resolveParamEffect. */
        static void applyDisruptionStackOrParamOnTile(GameState& state, int tilePos,
                                                      const std::pair<DisruptionEffect, int>& e);
};

#endif