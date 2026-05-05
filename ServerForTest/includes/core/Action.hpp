#ifndef ACTION_HPP
#define ACTION_HPP
#include <string>
#include <unordered_map>

/*
 * Action represents a player's request to the game.
 * 
 * The Round Controller validates WHO is sending it.
 * The Phase Handler validates WHAT is being done.
 *
 * The `type` field determines the action kind (e.g. "pass", "place_token", 
 * "trade", "claim_first_player", "resolve_disruption", etc.)
 * 
 * The `params` map holds action-specific data (e.g. target stack, token type, 
 * trade amounts, etc.)
 */

struct Action {
    int         playerId;
    std::string type;       // Action identifier (phase handler interprets this)
    std::unordered_map<std::string, std::string> params;

    // Convenience: check if this is a pass action
    bool isPass() const { return type == "pass"; }
};

#endif
