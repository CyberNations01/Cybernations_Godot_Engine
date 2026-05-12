#ifndef ROOM_HPP
#define ROOM_HPP

#include <functional>
#include <map>
#include <optional>
#include <string>

#include "game/GameRoom.hpp"
#include "core/Action.hpp"
#include "core/ActionResult.hpp"

enum ROOM_STATE {
    WAITING,
    PLAYING,
    FINISHED
};

class Room {
public:
    Room(std::function<void(int conn_id, std::string msg)> sendFunc)
    : sendFunc(sendFunc){};
    ~Room() = default;

    void joinPlayer(int conn_id);
    void onAction(int conn_id, Action action);
    ActionResult enableAutoPassForConnection(int conn_id);
    void removePlayer(int conn_id);
    void startGame();
    std::string getSnapshot() const;
    ROOM_STATE getRoomState() const { return roomState; }
    int getPlayerIdForConnection(int conn_id) const;
    
private:
    void broadcast(const std::string& msg);
    void send(int conn_id, const std::string& msg);

    std::string roomId;
    ROOM_STATE  roomState = ROOM_STATE::WAITING;
    GameRoom    gameRoom;
    int nextPlayerId = 0;
    std::map<int,int> conn_map;
    std::optional<int> autoPassManualPlayerId;
    std::function<void(int conn_id, std::string msg)> sendFunc;
    std::string serialize(ActionResult result);
    ActionResult applyAutoPassIfNeeded();
};

#endif
