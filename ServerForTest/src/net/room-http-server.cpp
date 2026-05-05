#include "net/http/httplib.h"
#include "nlohmann/json.hpp"
#include "net/Room.hpp"
#include "core/Action.hpp"

#include <iostream>
#include <map>
#include <sstream>
#include <string>
#include <vector>

using json = nlohmann::json;

namespace {

json parseMessage(const std::string& msg)
{
    json parsed = json::parse(msg, nullptr, false);
    return parsed.is_discarded() ? json(msg) : parsed;
}

json normalizedSnapshot(const Room& room)
{
    json snapshot = json::parse(room.getSnapshot(), nullptr, false);
    if (snapshot.is_discarded())
        return json::object();

    for (const auto& key : {"gameState", "controller"}) {
        if (snapshot.contains(key) && snapshot[key].is_string()) {
            json parsed = json::parse(snapshot[key].get<std::string>(), nullptr, false);
            if (!parsed.is_discarded())
                snapshot[key] = parsed;
        }
    }
    return snapshot;
}

std::string normalizeRoomMessage(const std::string& msg)
{
    json parsed = parseMessage(msg);
    if (!parsed.is_object())
        return msg;

    for (const auto& key : {"gameState", "controller"}) {
        if (parsed.contains(key) && parsed[key].is_string()) {
            json nested = json::parse(parsed[key].get<std::string>(), nullptr, false);
            if (!nested.is_discarded())
                parsed[key] = nested;
        }
    }
    return parsed.dump();
}

std::string roomStateToStr(ROOM_STATE state)
{
    switch (state) {
        case ROOM_STATE::WAITING: return "WAITING";
        case ROOM_STATE::PLAYING: return "PLAYING";
        case ROOM_STATE::FINISHED: return "FINISHED";
    }
    return "UNKNOWN";
}

json drainMessages(std::map<int, std::vector<std::string>>& outbox, int connId)
{
    json messages = json::array();
    auto it = outbox.find(connId);
    if (it == outbox.end())
        return messages;

    for (const auto& msg : it->second)
        messages.push_back(parseMessage(msg));
    it->second.clear();
    return messages;
}

}  // namespace

int main()
{
    httplib::Server server;
    std::map<int, std::vector<std::string>> outbox;
    std::map<std::string, int> sessionToConn;
    int nextConnId = 1;
    int nextSessionId = 1;

    Room room([&](int connId, std::string msg) {
        outbox[connId].push_back(normalizeRoomMessage(msg));
    });

    server.set_default_headers({
        {"Access-Control-Allow-Origin", "*"},
        {"Access-Control-Allow-Methods", "GET, POST, OPTIONS"},
        {"Access-Control-Allow-Headers", "Content-Type"}
    });

    server.Options(".*", [](const httplib::Request&, httplib::Response& res) {
        res.status = 204;
    });

    server.Get("/state", [&](const httplib::Request&, httplib::Response& res) {
        json response = normalizedSnapshot(room);
        response["roomState"] = roomStateToStr(room.getRoomState());
        res.set_content(response.dump(2), "application/json");
    });

    server.Post("/join", [&](const httplib::Request&, httplib::Response& res) {
        const int connId = nextConnId++;
        const std::string sessionId = "room-session-" + std::to_string(nextSessionId++);
        sessionToConn[sessionId] = connId;

        room.joinPlayer(connId);

        json response = {
            {"sessionId", sessionId},
            {"connId", connId},
            {"playerId", room.getPlayerIdForConnection(connId)},
            {"roomState", roomStateToStr(room.getRoomState())},
            {"messages", drainMessages(outbox, connId)}
        };
        response["snapshot"] = normalizedSnapshot(room);
        res.set_content(response.dump(2), "application/json");
    });

    server.Post("/start", [&](const httplib::Request& req, httplib::Response& res) {
        json body = json::parse(req.body, nullptr, false);
        if (body.is_discarded() || !body.contains("sessionId")) {
            res.status = 400;
            res.set_content(R"({"error":"missing sessionId"})", "application/json");
            return;
        }

        const std::string sessionId = body.value("sessionId", "");
        auto sessionIt = sessionToConn.find(sessionId);
        if (sessionIt == sessionToConn.end()) {
            res.status = 403;
            res.set_content(R"({"error":"unknown sessionId"})", "application/json");
            return;
        }

        room.startGame();
        const int connId = sessionIt->second;
        json response = {
            {"sessionId", sessionId},
            {"connId", connId},
            {"playerId", room.getPlayerIdForConnection(connId)},
            {"roomState", roomStateToStr(room.getRoomState())},
            {"messages", drainMessages(outbox, connId)}
        };
        response["snapshot"] = normalizedSnapshot(room);
        res.set_content(response.dump(2), "application/json");
    });

    server.Post("/action", [&](const httplib::Request& req, httplib::Response& res) {
        json body = json::parse(req.body, nullptr, false);
        if (body.is_discarded()) {
            res.status = 400;
            res.set_content(R"({"error":"invalid JSON"})", "application/json");
            return;
        }

        const std::string sessionId = body.value("sessionId", "");
        auto sessionIt = sessionToConn.find(sessionId);
        if (sessionIt == sessionToConn.end()) {
            res.status = 403;
            res.set_content(R"({"error":"unknown sessionId"})", "application/json");
            return;
        }

        Action action;
        action.type = body.value("type", "");
        if (action.type.empty()) {
            res.status = 400;
            res.set_content(R"({"error":"missing or invalid 'type'"})", "application/json");
            return;
        }
        if (body.contains("params") && body["params"].is_object()) {
            for (auto& [k, v] : body["params"].items())
                action.params[k] = v.is_string() ? v.get<std::string>() : v.dump();
        }

        const int connId = sessionIt->second;
        room.onAction(connId, action);

        json response = {
            {"sessionId", sessionId},
            {"connId", connId},
            {"playerId", room.getPlayerIdForConnection(connId)},
            {"roomState", roomStateToStr(room.getRoomState())},
            {"messages", drainMessages(outbox, connId)}
        };
        response["snapshot"] = normalizedSnapshot(room);
        res.set_content(response.dump(2), "application/json");
    });

    server.Get("/messages", [&](const httplib::Request& req, httplib::Response& res) {
        if (!req.has_param("sessionId")) {
            res.status = 400;
            res.set_content(R"({"error":"missing sessionId"})", "application/json");
            return;
        }
        const std::string sessionId = req.get_param_value("sessionId");
        auto sessionIt = sessionToConn.find(sessionId);
        if (sessionIt == sessionToConn.end()) {
            res.status = 403;
            res.set_content(R"({"error":"unknown sessionId"})", "application/json");
            return;
        }

        const int connId = sessionIt->second;
        json response = {
            {"sessionId", sessionId},
            {"connId", connId},
            {"playerId", room.getPlayerIdForConnection(connId)},
            {"roomState", roomStateToStr(room.getRoomState())},
            {"messages", drainMessages(outbox, connId)}
        };
        response["snapshot"] = normalizedSnapshot(room);
        res.set_content(response.dump(2), "application/json");
    });

    std::cout << "Cybernation room test server listening on 0.0.0.0:8081\n";
    server.listen("0.0.0.0", 8081);
}
