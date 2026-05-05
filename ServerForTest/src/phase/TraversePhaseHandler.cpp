#include "phase/TraversePhaseHandler.hpp"

ActionResult TraversePhaseHandler::handle(const Action& action, GameState& state)
{
    if (action.type == "draw_disruption")
        return handleDrawDisruption(state);

    if (action.type == "resolve_disruption")
        return handleResolveDisruption(action, state);

    if (action.type == "walk_path")
        return handleWalkPath(state);

    return {ActionStatus::INVALID_ACTION};
}

ActionResult TraversePhaseHandler::handleWalkPath(GameState &state)
{
    return GameUtility::walkPath(state);
}

ActionResult TraversePhaseHandler::handleDrawDisruption(GameState &state)
{
    return GameUtility::drawDisruption(state);;
}

ActionResult TraversePhaseHandler::handleResolveDisruption(const Action& action, GameState& state)
{
    return GameUtility::applyDisruptionEffect(state, action);
}