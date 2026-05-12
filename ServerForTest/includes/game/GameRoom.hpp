#ifndef GAME_ROOM_HPP
#define GAME_ROOM_HPP

#include "game/GameState.hpp"
#include "game/RoundController.hpp"
#include "core/Action.hpp"
#include "core/ActionResult.hpp"
#include <string>

/*
 * GameRoom — Top-level game session
 * 
 * Owns the GameState and the RoundController.
 * Provides a simple interface for the server layer:
 *   - receiveAction()  : Process a player action
 *   - getSnapshot()    : Get full game state as JSON string
 * 
 * The server layer (REST/WebSocket) just needs to:
 *   1. Parse the client request into an Action
 *   2. Call room.receiveAction(action)
 *   3. Send back room.getSnapshot() to all clients
 */

class GameRoom {
private:
    GameState       state;
    RoundController controller;
    /** Stable for this server process + GameRoom instance; changes after server restart. */
    std::string sessionId_;

public:
    GameRoom();
    ~GameRoom() = default;
    
    // Process a player action. Returns what happened.
    ActionResult receiveAction(const Action& action);
    ActionResult autoPassUntilPlayer(int manualPlayerId);

    std::string getSnapshot() const;
    std::string getControllerSnapshot() const;
    std::string getGameStateSnapshot() const;

    GameState& getState() { return state; }
    const RoundController& getController() const { return controller; }
    const std::string& getSessionId() const { return sessionId_; }
};

#endif
