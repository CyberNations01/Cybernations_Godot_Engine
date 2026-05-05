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
