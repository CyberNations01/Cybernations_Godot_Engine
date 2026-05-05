#include <iostream>
#include <string>
#include "game/GameState.hpp"
#include "game/GameUtility.hpp"
#include "core/Action.hpp"

namespace {
int gFailures = 0;

void expectTrue(bool cond, const std::string& label) {
    std::cout << (cond ? "[PASS] " : "[FAIL] ") << label << std::endl;
    if (!cond) ++gFailures;
}

DisruptionCard requireCardByName(const GameState& state, const std::string& name) {
    for (const auto& card : state.disruptionManager.getDeck()) {
        if (card.getName() == name) return card;
    }
    for (const auto& card : state.disruptionManager.getDiscard()) {
        if (card.getName() == name) return card;
    }
    std::cerr << "[FATAL] Cannot find card: " << name << std::endl;
    std::exit(2);
}

void prepRichResources(GameState& state) {
    state.params.setCohesion(25);
    state.params.setCybernationLevel(20);
    state.params.setHumanRelation(20);
    state.params.setTechnology(20);
    state.params.setEnvironment(20);
}

Action resolveAction() {
    Action a;
    a.playerId = 0;
    a.type = "resolve_disruption";
    return a;
}
}

int main() {
    std::cout << "=== Disruption Categories Test ===" << std::endl;

    // CatA
    {
        GameState s;
        prepRichResources(s);
        s.activeDisruption = requireCardByName(s, "HURRICANE_1");
        Action a = resolveAction();
        ActionResult r = GameUtility::applyDisruptionEffect(s, a);
        expectTrue(r.ok(), "CatA resolves");
        expectTrue(s.board[0].getEffectiveType() == StackType::WASTE, "CatA applies tile turn waste");
    }

    // CatB
    {
        GameState s;
        prepRichResources(s);
        const int before = s.params.getCohesion();
        s.activeDisruption = requireCardByName(s, "RISING_CRIME_1");
        ActionResult r = GameUtility::applyDisruptionEffect(s, resolveAction());
        expectTrue(r.ok(), "CatB resolves");
        expectTrue(s.params.getCohesion() == before - 2, "CatB applies cohesion loss");
    }

    // CatC
    {
        GameState s;
        prepRichResources(s);
        s.activeDisruption = requireCardByName(s, "CLIMATE_MIGRATION");
        const int before = s.params.getCohesion();
        ActionResult r = GameUtility::applyDisruptionEffect(s, resolveAction());
        expectTrue(r.ok(), "CatC resolves");
        expectTrue(s.params.getCohesion() == before - 1, "CatC applies param effect");
        expectTrue(s.board[0].getEffectiveType() == StackType::WASTE, "CatC applies tile effect");
    }

    // CatD
    {
        GameState s;
        prepRichResources(s);
        GameUtility::changeTileStack(s, 0, StackType::WILD);
        s.activeDisruption = requireCardByName(s, "GLACIAL_MELT");
        ActionResult r = GameUtility::applyDisruptionEffect(s, resolveAction());
        expectTrue(r.ok(), "CatD resolves");
        expectTrue(s.board[0].getEffectiveType() == StackType::WASTE, "CatD condition tile effect applies");
    }

    // CatE
    {
        GameState s;
        prepRichResources(s);
        const int before = s.params.getCybernationLevel();
        s.activeDisruption = requireCardByName(s, "SOCIETAL_SHIFT_1");
        ActionResult r = GameUtility::applyDisruptionEffect(s, resolveAction());
        expectTrue(r.ok(), "CatE resolves");
        expectTrue(s.params.getCybernationLevel() == before - 2, "CatE applies cancel cost branch");
    }

    // CatF
    {
        GameState s;
        prepRichResources(s);
        s.params.setCohesion(15);
        s.params.setHumanRelation(5);
        s.params.setTechnology(5);
        s.params.setEnvironment(5);
        const int beforeCo = s.params.getCohesion();
        const int beforeHR = s.params.getHumanRelation();
        const int beforeTech = s.params.getTechnology();
        const int beforeEnv = s.params.getEnvironment();
        s.activeDisruption = requireCardByName(s, "REBIRTH_OF_COMMUNITY_1");
        Action a = resolveAction();
        a.params["HR"] = "2";
        a.params["Tech"] = "2";
        a.params["Env"] = "1";
        ActionResult r = GameUtility::applyDisruptionEffect(s, a);
        expectTrue(r.ok(), "CatF resolves with distribution");
        expectTrue(s.params.getCohesion() == beforeCo + 2, "CatF applies fixed cohesion gain");
        expectTrue(s.params.getHumanRelation() == beforeHR + 2, "CatF distribution HR");
        expectTrue(s.params.getTechnology() == beforeTech + 2, "CatF distribution Tech");
        expectTrue(s.params.getEnvironment() == beforeEnv + 1, "CatF distribution Env");
    }

    // CatG
    {
        GameState s;
        prepRichResources(s);
        const int before = s.params.getHumanRelation();
        s.activeDisruption = requireCardByName(s, "FOCUSSED_RESEARCH_1");
        Action a = resolveAction();
        a.params["effectIndex"] = "0"; // HR +2 per developed stack
        ActionResult r = GameUtility::applyDisruptionEffect(s, a);
        expectTrue(r.ok(), "CatG resolves");
        expectTrue(s.params.getHumanRelation() > before, "CatG applies scaled gain");
    }

    // CatH (IgnoreCohesionEffect branch)
    {
        GameState s;
        prepRichResources(s);
        s.activeDisruption = requireCardByName(s, "EMPATHY_1");
        Action a = resolveAction();
        a.params["effectIndex"] = "1"; // IgnoreCohesionEffect
        ActionResult r = GameUtility::applyDisruptionEffect(s, a);
        expectTrue(r.ok(), "CatH resolves");
        expectTrue(s.ignoreCohesionLossThisRound, "CatH sets ignore cohesion loss flag");
    }

    // CatI
    {
        GameState s;
        prepRichResources(s);
        const int beforeHR = s.params.getHumanRelation();
        const int beforeTech = s.params.getTechnology();
        s.activeDisruption = requireCardByName(s, "TECHNOLOGY_BREAKTHROUGH_1");
        Action a = resolveAction();
        a.params["targetTile"] = "1";
        ActionResult r = GameUtility::applyDisruptionEffect(s, a);
        expectTrue(r.ok(), "CatI resolves");
        expectTrue(s.params.getHumanRelation() == beforeHR - 1, "CatI applies HR cost");
        expectTrue(s.params.getTechnology() == beforeTech - 1, "CatI applies Tech cost");
        expectTrue(s.board[1].hasOverlay(), "CatI places development overlay");
    }

    // CatJ
    {
        GameState s;
        prepRichResources(s);
        GameUtility::changeTileStack(s, 0, StackType::WASTE);
        const int beforeEnv = s.params.getEnvironment();
        const int beforeCy = s.params.getCybernationLevel();
        s.activeDisruption = requireCardByName(s, "BLOOM_COVERED_PLATEAUS");
        Action a = resolveAction();
        a.params["useOptional"] = "true";
        ActionResult r = GameUtility::applyDisruptionEffect(s, a);
        expectTrue(r.ok(), "CatJ resolves");
        expectTrue(s.board[0].getEffectiveType() == StackType::WILD, "CatJ turns Waste to Wild");
        expectTrue(s.params.getEnvironment() == beforeEnv - 1, "CatJ optional cost applied");
        expectTrue(s.params.getCybernationLevel() == beforeCy + 1, "CatJ optional gain applied");
    }

    // CatK currently removed from data/disruption.json by product decision.
    {
        GameState s;
        bool hasCatK = false;
        for (const auto& card : s.disruptionManager.getDeck()) {
            if (card.getCategory() == "CatK") {
                hasCatK = true;
                break;
            }
        }
        expectTrue(!hasCatK, "CatK cards are absent in disruption data");
    }

    std::cout << "=== Disruption Categories Finished ===" << std::endl;
    std::cout << "Total failures: " << gFailures << std::endl;
    return gFailures == 0 ? 0 : 1;
}
