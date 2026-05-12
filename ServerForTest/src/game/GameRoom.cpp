#include "game/GameRoom.hpp"
#include <chrono>
#include <random>
#include <sstream>

GameRoom::GameRoom()
    : state(), controller(state)
{
    auto ticks = std::chrono::high_resolution_clock::now().time_since_epoch().count();
    std::random_device rd;
    std::stringstream ss;
    ss << std::hex << ticks << '-' << rd() << '-' << rd();
    sessionId_ = ss.str();
}

ActionResult GameRoom::receiveAction(const Action& action)
{
    return controller.processAction(action, state);
}

ActionResult GameRoom::autoPassUntilPlayer(int manualPlayerId)
{
    if (manualPlayerId < 0 || manualPlayerId >= GameState::NUM_PLAYERS) {
        return ActionResult::invalid("Invalid manual player id for auto pass");
    }

    int guard = GameState::NUM_PLAYERS + 1;
    while (guard-- > 0 &&
           controller.getCurrentPhase() == GamePhase::ENVISION &&
           controller.getCurrentPlayerId() != manualPlayerId &&
           !controller.isGameOver()) {
        Action passAction;
        passAction.playerId = controller.getCurrentPlayerId();
        passAction.type = "pass";

        auto result = controller.processAction(passAction, state);
        if (!result.ok()) {
            return result;
        }
    }

    return ActionResult::success({"auto_pass", "Auto pass applied"});
}

std::string GameRoom::getSnapshot() const
{
    nlohmann::json combined;
    combined["gameState"]  = getGameStateSnapshot();
    combined["controller"] = getControllerSnapshot();
    combined["sessionId"] = sessionId_;
    return combined.dump(2);
}

std::string GameRoom::getControllerSnapshot() const
{
    return controller.toJson().dump(2);
}

std::string GameRoom::getGameStateSnapshot() const
{
    return state.toJson().dump(2);
}
