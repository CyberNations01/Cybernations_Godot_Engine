#include <iostream>
#include <string>
#include "phase/TraversePhaseHandler.hpp"
#include "game/GameState.hpp"
#include "core/Action.hpp"

namespace {
int gFailures = 0;

void expectTrue(bool cond, const std::string& label) {
    std::cout << (cond ? "[PASS] " : "[FAIL] ") << label << std::endl;
    if (!cond) ++gFailures;
}

Action mk(const std::string& type) {
    Action a;
    a.playerId = 0;
    a.type = type;
    return a;
}

DisruptionCard mustFindCard(const GameState& state, const std::string& name) {
    for (const auto& card : state.disruptionManager.getDeck()) {
        if (card.getName() == name) return card;
    }
    for (const auto& card : state.disruptionManager.getDiscard()) {
        if (card.getName() == name) return card;
    }
    std::cerr << "[FATAL] missing card: " << name << std::endl;
    std::exit(2);
}
}

int main() {
    std::cout << "=== Traverse Disruption Flow Test ===" << std::endl;
    TraversePhaseHandler handler;

    {
        GameState s;
        s.params.setCohesion(25);
        s.params.setCybernationLevel(20);
        s.params.setHumanRelation(20);
        s.params.setTechnology(20);
        s.params.setEnvironment(20);

        Action resolveBeforeDraw = mk("resolve_disruption");
        ActionResult r1 = handler.handle(resolveBeforeDraw, s);
        expectTrue(!r1.ok(), "Cannot resolve before draw");

        Action draw = mk("draw_disruption");
        ActionResult r2 = handler.handle(draw, s);
        expectTrue(r2.ok(), "Draw disruption succeeds");
        expectTrue(s.activeDisruption.has_value(), "Active disruption after draw");

        Action drawAgain = mk("draw_disruption");
        ActionResult r3 = handler.handle(drawAgain, s);
        expectTrue(!r3.ok(), "Second draw rejected while card active");

        // Deck order is not fixed — use a known CatA so plain resolve {} always applies.
        s.activeDisruption = mustFindCard(s, "WILDFIRES_1");

        Action resolve = mk("resolve_disruption");
        ActionResult r4 = handler.handle(resolve, s);
        expectTrue(r4.ok(), "Resolve disruption succeeds after draw");
        expectTrue(!s.activeDisruption.has_value(), "Resolve clears active disruption");

        Action walk = mk("walk_path");
        ActionResult r5 = handler.handle(walk, s);
        expectTrue(r5.ok(), "walk_path succeeds after resolve");
    }

    {
        GameState s;
        s.params.setCybernationLevel(20);
        Action cancelBeforeDraw = mk("resolve_disruption");
        cancelBeforeDraw.params["cancel"] = "1";
        ActionResult r1 = handler.handle(cancelBeforeDraw, s);
        expectTrue(!r1.ok(), "Cannot resolve+cancel before draw");

        s.activeDisruption = mustFindCard(s, "HURRICANE_1");

        Action cancel = mk("resolve_disruption");
        cancel.params["cancel"] = "1";
        ActionResult r3 = handler.handle(cancel, s);
        expectTrue(r3.ok(), "Resolve+cancel succeeds");
        expectTrue(!s.activeDisruption.has_value(), "Resolve+cancel clears active disruption");
    }

    std::cout << "=== Traverse Disruption Finished ===" << std::endl;
    std::cout << "Total failures: " << gFailures << std::endl;
    return gFailures == 0 ? 0 : 1;
}
