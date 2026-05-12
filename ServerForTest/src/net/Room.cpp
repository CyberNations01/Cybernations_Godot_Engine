#include "net/Room.hpp"

void Room::joinPlayer(int conn_id)
{
    if (conn_map.find(conn_id) != conn_map.end()) {
        return send(conn_id,
            serialize({ActionStatus::INVALID_ACTION, {"Room", "Error: Player has joined"}}));
    }
    if (nextPlayerId >= GameState::NUM_PLAYERS) {
        return send(conn_id,
            serialize({ActionStatus::INVALID_ACTION, {"Room", "Room is full"}}));
    }
    conn_map.insert({conn_id, nextPlayerId});
    return send(conn_id,
            serialize(ActionResult::success({"Room", 
                                             "PlayerId: " + std::to_string(nextPlayerId++)})));
}

void Room::onAction(int conn_id, Action action)
{
    if (roomState != ROOM_STATE::PLAYING)
        return send(conn_id, serialize({ActionStatus::INVALID_ACTION, {"Room", "Game not started"}}));

    auto it = conn_map.find(conn_id);
    if (it == conn_map.end())
        return send(conn_id, serialize({ActionStatus::INVALID_ACTION, {"Room", "Unknown connection"}}));

    // Bind action identity to the connection; clients cannot spoof playerId.
    action.playerId = it->second;
    auto result = gameRoom.receiveAction(action);
    if (result.ok()) {
        auto autoPassResult = applyAutoPassIfNeeded();
        if (!autoPassResult.ok()) {
            result = autoPassResult;
        }
    }
    if (result.ok())
        broadcast(gameRoom.getSnapshot());

    send(conn_id, serialize(result));
}

ActionResult Room::enableAutoPassForConnection(int conn_id)
{
    auto it = conn_map.find(conn_id);
    if (it == conn_map.end())
        return {ActionStatus::INVALID_ACTION, {"Room", "Unknown connection"}};

    autoPassManualPlayerId = it->second;
    return applyAutoPassIfNeeded();
}

void Room::startGame()
{
    roomState = ROOM_STATE::PLAYING;
    applyAutoPassIfNeeded();
    broadcast(gameRoom.getSnapshot());
}

std::string Room::getSnapshot() const
{
    return gameRoom.getSnapshot();
}

int Room::getPlayerIdForConnection(int conn_id) const
{
    auto it = conn_map.find(conn_id);
    if (it == conn_map.end())
        return -1;
    return it->second;
}


// TODO: handle graceful player disconnect 
// (auto-pass, switch first player, and also update next player etc.)
void Room::removePlayer(int conn_id)
{
    if (conn_map.find(conn_id) == conn_map.end())
        std::cout << "Room: " << "Invalid connection ID" << std::endl;
    
    conn_map.erase(conn_id);
}

void Room::broadcast(const std::string &msg)
{
    for (const auto& e: conn_map)
        sendFunc(e.first, msg);
}

void Room::send(int conn_id, const std::string &msg)
{
    sendFunc(conn_id, msg);
}

ActionResult Room::applyAutoPassIfNeeded()
{
    if (roomState != ROOM_STATE::PLAYING || !autoPassManualPlayerId.has_value())
        return ActionResult::success();

    return gameRoom.autoPassUntilPlayer(autoPassManualPlayerId.value());
}

std::string Room::serialize(ActionResult result)
{
    nlohmann::json j;
    j["status"] = actionStatusToStr(result.status);
    j["type"] = result.message.type;
    j["payload"] = result.message.payload;
    return j.dump(2);
}
