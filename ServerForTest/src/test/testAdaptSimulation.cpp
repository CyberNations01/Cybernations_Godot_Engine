#include <iostream>
#include <string>
#include <vector>
#include "nlohmann/json.hpp"
#include "phase/AdoptPhaseHandler.hpp"
#include "game/GameState.hpp"
#include "core/Action.hpp"
#include "core/DisruptionCard.hpp"
#include "core/Stack.hpp"
#include "game/GameUtility.hpp"

// Compare Adopt::resolve_disruption with GameUtility::applyDisruptionEffect (same state + action).
static bool sameResolveOutcome(AdoptPhaseHandler& adopt, GameState& s, const Action& a) {
    GameState sU = s;
    ActionResult rU = GameUtility::applyDisruptionEffect(sU, a);
    ActionResult rA = adopt.handle(a, s);
    return rU.ok() == rA.ok();
}

namespace {
int gFailures = 0;

void expectTrue(bool cond, const std::string& label) {
    std::cout << (cond ? "[PASS] " : "[FAIL] ") << label << std::endl;
    if (!cond) ++gFailures;
}

Action makeAdoptAction(const std::string& type) {
    Action a;
    a.playerId = 0;
    a.type = type;
    return a;
}

ActionResult run(AdoptPhaseHandler& handler, GameState& state, Action action, const std::string& label) {
    ActionResult r = handler.handle(action, state);
    std::cout << "[ACTION " << label << "] " << (r.ok() ? "OK" : "FAIL")
              << " status=" << static_cast<int>(r.status) << " type=" << r.message.type << std::endl;
    return r;
}

void prepState(GameState& s) {
    s.params.setCohesion(15);
    s.params.setCybernationLevel(5);
    s.params.setHumanRelation(20);
    s.params.setTechnology(20);
    s.params.setEnvironment(20);
}

void prepRichResources(GameState& s) {
    s.params.setCohesion(25);
    s.params.setCybernationLevel(20);
    s.params.setHumanRelation(20);
    s.params.setTechnology(20);
    s.params.setEnvironment(20);
}

void forceTrack(GameState& state, const std::vector<TokenEffect>& track) {
    state.activeDisruption = std::nullopt;
    state.adaptTrack = track;
    state.adaptCursor = 0;
    state.tokenManager.clearTrack();
    state.setTokenBag(track);
}

DisruptionCard requireCardByName(const GameState& state, const std::string& name) {
    for (const auto& card : state.disruptionManager.getDeck()) {
        if (card.getName() == name) return card;
    }
    for (const auto& card : state.disruptionManager.getDiscard()) {
        if (card.getName() == name) return card;
    }
    std::cerr << "[FATAL] missing card: " << name << std::endl;
    std::exit(2);
}
}  // namespace

int main() {
    std::cout << "=== AdoptPhaseHandler direct tests (no GameRoom) ===" << std::endl;
    AdoptPhaseHandler adopt;
    GameState state;
    prepState(state);

    // --- 1) resolve_feedback validation
    forceTrack(state, {TokenEffect::TURN_WILD});
    expectTrue(!run(adopt, state, makeAdoptAction("resolve_feedback"), "missing_params").ok(),
               "resolve_feedback requires target_tile, decision");

    forceTrack(state, {TokenEffect::TURN_WILD});
    {
        Action a = makeAdoptAction("resolve_feedback");
        a.params["decision"] = "allow";
        a.params["target_tile"] = "abc";
        expectTrue(!run(adopt, state, a, "bad_tile").ok(), "invalid target_tile");
    }

    forceTrack(state, {TokenEffect::TURN_WILD});
    {
        Action a = makeAdoptAction("resolve_feedback");
        a.params["decision"] = "allow";
        a.params["target_tile"] = "1";
        expectTrue(!run(adopt, state, a, "wrong_ring").ok(), "slot0 must be inner 0 only");
    }

    forceTrack(state, {TokenEffect::TURN_WILD, TokenEffect::TURN_WILD, TokenEffect::TURN_WILD});
    {
        Action a = makeAdoptAction("resolve_feedback");
        a.params["decision"] = "allow";
        a.params["target_tile"] = "0";
        expectTrue(run(adopt, state, a, "inner0").ok(), "inner 0");
    }
    {
        Action a = makeAdoptAction("resolve_feedback");
        a.params["decision"] = "allow";
        a.params["target_tile"] = "1";
        expectTrue(run(adopt, state, a, "mid1").ok(), "first middle 1");
    }
    {
        Action a = makeAdoptAction("resolve_feedback");
        a.params["decision"] = "allow";
        a.params["target_tile"] = "1";
        expectTrue(!run(adopt, state, a, "reuse").ok(), "no duplicate stack");
    }

    forceTrack(state, {TokenEffect::TURN_WILD});
    state.params.setCybernationLevel(0);
    {
        Action a = makeAdoptAction("resolve_feedback");
        a.params["decision"] = "cancel";
        a.params["target_tile"] = "0";
        expectTrue(!run(adopt, state, a, "cancel_no_cy").ok(), "cancel needs Cy>=1");
    }

    forceTrack(state, {TokenEffect::TURN_WILD});
    state.params.setCybernationLevel(2);
    {
        Action a = makeAdoptAction("resolve_feedback");
        a.params["decision"] = "cancel";
        a.params["target_tile"] = "0";
        expectTrue(run(adopt, state, a, "cancel_ok").ok(), "cancel ok");
    }
    expectTrue(state.params.getCybernationLevel() == 1, "deducts 1 Cy for cancel");

    // --- 2) Token effect branches
    forceTrack(state, {TokenEffect::DEVELOP_STACK});
    state.getTile(0)->removeOverlay();
    {
        Action a = makeAdoptAction("resolve_feedback");
        a.params["decision"] = "allow";
        a.params["target_tile"] = "0";
        a.params["develop_to"] = "DEV_B";
        expectTrue(run(adopt, state, a, "dev").ok(), "DEVELOP_STACK");
        expectTrue(state.getTile(0)->getOverlay().getType() == StackType::DEV_B, "DEV_B");
    }

    forceTrack(state, {TokenEffect::TRANSFORM_STACK});
    {
        Stack ov;
        ov.setType(StackType::DEV_A);
        state.getTile(0)->setOverlay(ov);
    }
    {
        Action a = makeAdoptAction("resolve_feedback");
        a.params["decision"] = "allow";
        a.params["target_tile"] = "0";
        expectTrue(run(adopt, state, a, "transform").ok(), "TRANSFORM");
        expectTrue(state.getTile(0)->getOverlay().getType() == StackType::DEV_B, "toggle to DEV_B");
    }

    forceTrack(state, {TokenEffect::UNKNOWN});
    {
        Action a = makeAdoptAction("resolve_feedback");
        a.params["decision"] = "allow";
        a.params["target_tile"] = "0";
        expectTrue(run(adopt, state, a, "unknown").ok(), "UNKNOWN");
    }

    forceTrack(state, {TokenEffect::SOLVE_DISRUPTION});
    {
        Action a = makeAdoptAction("resolve_feedback");
        a.params["decision"] = "allow";
        a.params["target_tile"] = "0";
        expectTrue(run(adopt, state, a, "solve").ok(), "SOLVE_DISRUPTION draws");
        expectTrue(state.activeDisruption.has_value(), "active after draw");
    }

    // --- 3) resolve_disruption + disruption_name
    forceTrack(state, {TokenEffect::TURN_WILD});
    expectTrue(!run(adopt, state, makeAdoptAction("resolve_disruption"), "no_active").ok(),
               "no active and no name");

    forceTrack(state, {TokenEffect::TURN_WILD});
    {
        Action a = makeAdoptAction("resolve_disruption");
        a.params["disruption_name"] = "___NONE___";
        expectTrue(!run(adopt, state, a, "bad_name").ok(), "unknown name");
    }

    forceTrack(state, {TokenEffect::TURN_WILD});
    {
        Action a = makeAdoptAction("resolve_disruption");
        a.params["disruption_name"] = "COMMUNITY_LEADERS_1";
        a.params["decision"] = "cancel";
        expectTrue(!run(adopt, state, a, "catH_cancel").ok(), "CatH needs effectIndex; fails as-is");
    }

    forceTrack(state, {TokenEffect::TURN_WILD});
    {
        Action a = makeAdoptAction("resolve_disruption");
        a.params["disruption_name"] = "HURRICANE_1";
        a.params["times"] = "abc";
        expectTrue(!run(adopt, state, a, "bad_times").ok(), "times int parse");
    }

    forceTrack(state, {TokenEffect::TURN_WILD});
    {
        Action a = makeAdoptAction("resolve_disruption");
        a.params["disruption_name"] = "HURRICANE_1";
        a.params["times"] = "0";
        expectTrue(!run(adopt, state, a, "times0").ok(), "times > 0");
    }

    forceTrack(state, {TokenEffect::TURN_WILD});
    {
        Action a = makeAdoptAction("resolve_disruption");
        a.params["disruption_name"] = "HURRICANE_1";
        ActionResult r = run(adopt, state, a, "named_hurricane");
        expectTrue(r.ok() && r.message.type == "disruption_effect_cancelled", "named default cancel");
    }

    forceTrack(state, {TokenEffect::TURN_WILD});
    state.params.setCybernationLevel(5);
    {
        Action a = makeAdoptAction("resolve_disruption");
        a.params["disruption_name"] = "HURRICANE_1";
        a.params["decision"] = "apply";
        expectTrue(run(adopt, state, a, "named_apply").ok(), "named apply");
    }

    // --- 4) ADOPT should reject standalone draw_disruption in controlled flow
    forceTrack(state, {TokenEffect::TURN_WILD});
    expectTrue(!run(adopt, state, makeAdoptAction("draw_disruption"), "draw").ok(),
               "draw_disruption is not valid in Adopt");

    forceTrack(state, {TokenEffect::TURN_WILD});
    state.params.setCybernationLevel(10);
    state.activeDisruption = requireCardByName(state, "HURRICANE_1");
    {
        Action a = makeAdoptAction("resolve_disruption");
        a.params["cancel"] = "1";
        expectTrue(run(adopt, state, a, "cancel_known").ok(),
                   "resolve_disruption+cancel routes to applyDisruptionEffect(cancel=1)");
    }
    forceTrack(state, {TokenEffect::TURN_WILD});
    state.activeDisruption = requireCardByName(state, "HURRICANE_1");
    state.params.setCybernationLevel(20);
    expectTrue(run(adopt, state, makeAdoptAction("resolve_disruption"), "resolve_plain").ok(),
               "resolve_disruption without extra params (CatA applies)");

    // --- 4b) resolve_disruption: Adopt matches GameUtility (README / per-category params)
    {
        GameState s;
        prepRichResources(s);
        forceTrack(s, {TokenEffect::TURN_WILD});
        s.activeDisruption = requireCardByName(s, "HURRICANE_1");
        expectTrue(sameResolveOutcome(adopt, s, makeAdoptAction("resolve_disruption")), "CatA Adopt=Utility");
    }
    {
        GameState s;
        prepRichResources(s);
        forceTrack(s, {TokenEffect::TURN_WILD});
        s.activeDisruption = requireCardByName(s, "RISING_CRIME_1");
        expectTrue(sameResolveOutcome(adopt, s, makeAdoptAction("resolve_disruption")), "CatB");
    }
    {
        GameState s;
        prepRichResources(s);
        forceTrack(s, {TokenEffect::TURN_WILD});
        s.activeDisruption = requireCardByName(s, "CLIMATE_MIGRATION");
        expectTrue(sameResolveOutcome(adopt, s, makeAdoptAction("resolve_disruption")), "CatC");
    }
    {
        GameState s;
        prepRichResources(s);
        forceTrack(s, {TokenEffect::TURN_WILD});
        GameUtility::changeTileStack(s, 0, StackType::WILD);
        s.activeDisruption = requireCardByName(s, "GLACIAL_MELT");
        expectTrue(sameResolveOutcome(adopt, s, makeAdoptAction("resolve_disruption")), "CatD");
    }
    {
        GameState s;
        prepRichResources(s);
        forceTrack(s, {TokenEffect::TURN_WILD});
        s.activeDisruption = requireCardByName(s, "SOCIETAL_SHIFT_1");
        expectTrue(sameResolveOutcome(adopt, s, makeAdoptAction("resolve_disruption")), "CatE");
    }
    {
        GameState s;
        prepRichResources(s);
        forceTrack(s, {TokenEffect::TURN_WILD});
        s.params.setCohesion(15);
        s.params.setHumanRelation(5);
        s.params.setTechnology(5);
        s.params.setEnvironment(5);
        s.activeDisruption = requireCardByName(s, "REBIRTH_OF_COMMUNITY_1");
        Action a = makeAdoptAction("resolve_disruption");
        a.params["HR"] = "2";
        a.params["Tech"] = "2";
        a.params["Env"] = "1";
        expectTrue(sameResolveOutcome(adopt, s, a), "CatF distribution params");
    }
    {
        GameState s;
        prepRichResources(s);
        forceTrack(s, {TokenEffect::TURN_WILD});
        s.activeDisruption = requireCardByName(s, "FOCUSSED_RESEARCH_1");
        Action a = makeAdoptAction("resolve_disruption");
        a.params["effectIndex"] = "0";
        expectTrue(sameResolveOutcome(adopt, s, a), "CatG effectIndex");
    }
    {
        GameState s;
        prepRichResources(s);
        forceTrack(s, {TokenEffect::TURN_WILD});
        s.activeDisruption = requireCardByName(s, "EMPATHY_1");
        Action a = makeAdoptAction("resolve_disruption");
        a.params["effectIndex"] = "1";
        expectTrue(sameResolveOutcome(adopt, s, a), "CatH effectIndex");
    }
    {
        GameState s;
        prepRichResources(s);
        forceTrack(s, {TokenEffect::TURN_WILD});
        s.activeDisruption = requireCardByName(s, "TECHNOLOGY_BREAKTHROUGH_1");
        Action a = makeAdoptAction("resolve_disruption");
        a.params["targetTiles"] = "1";
        expectTrue(sameResolveOutcome(adopt, s, a), "CatI targetTiles");
    }
    {
        GameState s;
        prepRichResources(s);
        forceTrack(s, {TokenEffect::TURN_WILD});
        GameUtility::changeTileStack(s, 0, StackType::WASTE);
        s.activeDisruption = requireCardByName(s, "BLOOM_COVERED_PLATEAUS");
        Action a = makeAdoptAction("resolve_disruption");
        a.params["useOptional"] = "1";
        expectTrue(sameResolveOutcome(adopt, s, a), "CatJ useOptional");
    }
    {
        // README example shape (subset): cancel + canc tiles + effectIndex + ppl + targetTiles + useOptional
        GameState s;
        prepRichResources(s);
        forceTrack(s, {TokenEffect::TURN_WILD});
        s.activeDisruption = requireCardByName(s, "HURRICANE_1");
        Action a = makeAdoptAction("resolve_disruption");
        a.params["canceltiles"] = "0,1";
        a.params["cancel"] = "1";
        expectTrue(sameResolveOutcome(adopt, s, a), "README-style combined params (CatA cancel path)");
    }

    // --- 5) invalid types removed from Adopt
    forceTrack(state, {TokenEffect::TURN_WILD});
    expectTrue(!run(adopt, state, makeAdoptAction("trade"), "trade").ok(), "no trade in Adopt");
    expectTrue(!run(adopt, state, makeAdoptAction("commit"), "commit").ok(), "no commit");

    // --- 6) last resolve_feedback => adaptPhaseCleanup
    forceTrack(state, {TokenEffect::TURN_WILD});
    {
        Action a = makeAdoptAction("resolve_feedback");
        a.params["decision"] = "allow";
        a.params["target_tile"] = "0";
        ActionResult r = run(adopt, state, a, "last_token");
        expectTrue(r.ok() && r.message.type == "adapt_feedback_resolved", "final message");
        nlohmann::json pl = nlohmann::json::parse(r.message.payload);
        expectTrue(pl.contains("adaptPhaseCleanup") && pl["adaptPhaseCleanup"]["adaptTrackFinalized"] == true,
                   "cleanup in payload");
    }
    expectTrue(state.adaptTrack.empty() && state.adaptCursor == 0, "track reset");

    forceTrack(state, {TokenEffect::TURN_WILD});
    {
        Action a;
        a.playerId = 0;
        a.type = "pass";
        expectTrue(!run(adopt, state, a, "pass_rejected").ok(), "pass not allowed in Adopt");
    }

    std::cout << "=== Finished ===" << std::endl;
    std::cout << "Total failures: " << gFailures << std::endl;
    return gFailures == 0 ? 0 : 1;
}
