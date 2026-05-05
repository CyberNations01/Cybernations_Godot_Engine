#include "net/Room.hpp"

class RoomManager{
public:
    static constexpr int code_size = 6; 

    RoomManager(std::function<void(int conn_id, std::string msg)> sendFunc)
    :sendFunc(sendFunc){};
    
    std::string createRoom();
    void joinRoom(std::string room_id, int conn_id);
    void leaveRoom(std::string room_id, int conn_id);
    const Room* getRoom(std::string room_id) const;

private:
    std::string generateCode(int length);
    std::map<std::string, Room> rooms;
    std::function<void(int, std::string)> sendFunc;
};