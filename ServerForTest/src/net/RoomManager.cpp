#include "net/RoomManager.hpp"

std::string
RoomManager::createRoom()
{
    std::string code;
    do {
        code = generateCode(code_size);
    } while (rooms.find(code) != rooms.end());

    rooms.insert({code, Room(sendFunc)});
    return code;
    
}
void RoomManager::joinRoom(std::string code, int conn_id)
{
    if (rooms.find(code) == rooms.end())
        return sendFunc(conn_id, "Game room does not exist");

    rooms.at(code).joinPlayer(conn_id);
}

void RoomManager::leaveRoom(std::string code, int conn_id)
{
    if (rooms.find(code) == rooms.end())
        return sendFunc(conn_id, "Failed to leave game room");

    rooms.at(code).removePlayer(conn_id);
    // TODO: if room is FINISHED or empty, erase from rooms map
}

const Room *RoomManager::getRoom(std::string code) const
{
    auto it = rooms.find(code);
    if (it == rooms.end())
        return nullptr;
    return &it->second;

}
std::string RoomManager::generateCode(int length)
{
    static const char chars[] = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    static std::mt19937 rng(std::random_device{}());
    std::uniform_int_distribution<int> dist(0, sizeof(chars) - 2);

    std::string code(length, ' ');
    for (auto& c : code)
            c = chars[dist(rng)];
    return code;
} 
