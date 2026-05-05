#ifndef ACTION_RESULT_HPP
#define ACTION_RESULT_HPP
#include <string>
#include "Types.hpp"

struct ActionMessage {
    std::string type;
    std::string payload;

    ActionMessage() = default;
    ActionMessage(const std::string& t, const std::string& p)
    : type(t), payload(p) {}
};

struct ActionResult {
    ActionStatus status;
    ActionMessage message;

    ActionResult(ActionStatus s):  status(s){};
    ActionResult(ActionStatus s, ActionMessage m) : status(s), message(m){};

    bool ok() const { return status == ActionStatus::SUCCESS;}

    static ActionResult success() {
        return {ActionStatus::SUCCESS};
    }

    static ActionResult success(const ActionMessage& msg) {
        return {ActionStatus::SUCCESS, msg}; 
    }

    static ActionResult ignored() {
        return {ActionStatus::NOT_YOUR_TURN};
    }

    static ActionResult alreadyPassed() {
        return {ActionStatus::PLAYER_ALREADY_PASSED};
    }
    
    static ActionResult invalid(const std::string& reason) {
        return {ActionStatus::INVALID_ACTION, {"error", reason}};
    }

};

#endif
