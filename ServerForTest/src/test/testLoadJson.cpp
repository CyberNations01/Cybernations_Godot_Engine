#include "core/DataLoader.hpp"
#include "core/Stack.hpp"
#include "core/Types.hpp"
#include "game/GameState.hpp"
#include "core/ActionResult.hpp"
#include "phase/TraversePhaseHandler.hpp"
#include "game/GameUtility.hpp"
#include <unordered_map>
#include <iostream>

void testLoadStack()
{
    std::vector<Stack> stackSet;
    stackSet = DataLoader::loadStack("data/stack.json");

    for (const auto& stack : stackSet) {
        std::cout << "=== Stack " << stack.getId() << " ===" << std::endl;
        std::cout << "Type: " << stackTypeToStr(stack.getType()) << std::endl;

        std::cout << "Sides:" << std::endl;
        const auto& sides = stack.getSides();
        for (int i = 0; i < (int)sides.size(); i++) {
            std::cout << "  Side " << i << ": [";
            for (int j = 0; j < (int)sides[i].size(); j++) {
                if (j > 0) std::cout << ", ";
                std::cout << sides[i][j];
            }
            std::cout << "]" << std::endl;
        }

        std::cout << "Paths:" << std::endl;
        const auto& paths = stack.getPaths();
        for (const auto& [from, to] : paths) {
            std::cout << "  " << from << " -> " << to << std::endl;
        }

        std::cout << std::endl;
    }
}

void testLoadTile()
{
    std::vector<Tile> board;
    board = DataLoader::loadTile("data/layout.json");

    for (const auto& tile : board) {
        std::cout << "=== Tile Position " << tile.getPosition() << " ===" << std::endl;
        std::cout << "Rotation: " << tile.getRotation() << std::endl;

        std::cout << "Neighbours:" << std::endl;
        const auto& neighbours = tile.getNeighbours();
        for (int i = 0; i < Tile::TILE_SIDES; i++) {
            std::cout << "  Side " << i << " -> ";
            if (neighbours[i].first == -1)
                std::cout << "EDGE";
            else
                std::cout << "Tile " << neighbours[i].first
                          << " (side " << neighbours[i].second << ")";
            std::cout << std::endl;
        }

        std::cout << std::endl;
    }
}

void testBoard()
{
    GameState state;
    const auto& board = state.getBoard();

    for (int i = 0; i < (int)board.size(); i++) {
        std::cout << "=== Tile " << board[i].getPosition() << " ===" << std::endl;

        std::cout << "Base: " << stackTypeToStr(board[i].getBase().getType())
                  << " (Stack " << board[i].getBase().getId() << ")" << std::endl;

        if (board[i].hasOverlay()) {
            std::cout << "Overlay: " << stackTypeToStr(board[i].getOverlay().getType())
                      << " (Stack " << board[i].getOverlay().getId() << ")" << std::endl;
        } else {
            std::cout << "Overlay: none" << std::endl;
        }

        std::cout << "Effective Type: "
                  << stackTypeToStr(board[i].getEffectiveType()) << std::endl;

        std::cout << "Base Sides:" << std::endl;
        const auto& sides = board[i].getBase().getSides();
        for (int s = 0; s < (int)sides.size(); s++) {
            std::cout << "  Side " << s << ": [";
            for (int j = 0; j < (int)sides[s].size(); j++) {
                if (j > 0) std::cout << ", ";
                std::cout << sides[s][j];
            }
            std::cout << "]" << std::endl;
        }

        if (board[i].hasOverlay()) {
            std::cout << "Overlay Sides:" << std::endl;
            const auto& oSides = board[i].getOverlay().getSides();
            for (int s = 0; s < (int)oSides.size(); s++) {
                std::cout << "  Side " << s << ": [";
                for (int j = 0; j < (int)oSides[s].size(); j++) {
                    if (j > 0) std::cout << ", ";
                    std::cout << oSides[s][j];
                }
                std::cout << "]" << std::endl;
            }
        }

        std::cout << "Neighbours:" << std::endl;
        const auto& neighbours = board[i].getNeighbours();
        for (int s = 0; s < Tile::TILE_SIDES; s++) {
            std::cout << "  Side " << s << ": ";
            if (neighbours[s].first == -1)
                std::cout << "EDGE";
            else
                std::cout << "Tile " << neighbours[s].first
                          << " (side " << neighbours[s].second << ")";
            std::cout << std::endl;
        }

        std::cout << "Active Paths:" << std::endl;
        if (board[i].hasOverlay()) {
            const auto& paths = board[i].getOverlay().getPaths();
            for (const auto& [from, to] : paths) {
                if (from < to)
                    std::cout << "  " << from << " <-> " << to << std::endl;
            }
        } else {
            const auto& paths = board[i].getBase().getPaths();
            for (const auto& [from, to] : paths) {
                if (from < to)
                    std::cout << "  " << from << " <-> " << to << std::endl;
            }
        }

        std::cout << std::endl;

    }
}

void testWalkPath()
{
    GameState state;
    TraversePhaseHandler handler;
    Action clientReq = {
        .playerId = 1,
        .type = "walkPath",
        .params = std::unordered_map<std::string, std::string>(),
    };

    ActionResult res = handler.handle(clientReq, state);
    std::cout << "Result Type: " << res.message.type << std::endl;
    std::cout << res.message.payload << std::endl;
}

void testLoadGoal()
{
    std::vector<Goal> goals = DataLoader::loadGoal("data/goal.json");

    for (const auto& goal : goals) {
        std::cout << "=== Goal " << goal.getId() << " ===" << std::endl;
        std::cout << "Name: " << goal.getName() << std::endl;
        std::cout << "Reverse Goal ID: " << goal.getReverseGoalId() << std::endl;

        std::cout << "Victory Conditions:" << std::endl;
        for (const auto& vc : goal.getConditions()) {
            std::cout << "  Type: " << vc.type
                      << " | Op: " << comparatorToStr(vc.op)
                      << " | Num: " << vc.num;
            if (vc.position.has_value())
                std::cout << " | Position: " << vc.position.value();
            std::cout << std::endl;
        }

        std::cout << "Stack Effects:" << std::endl;
        for (const auto& [stackType, values] : goal.getStackEffect()) {
            std::cout << "  " << stackTypeToStr(stackType) << ": [";
            for (int i = 0; i < (int)values.size(); i++) {
                if (i > 0) std::cout << ", ";
                std::cout << values[i];
            }
            std::cout << "]" << std::endl;
        }

        std::cout << std::endl;
    }
}

void testDraw10AndResolve() {
    GameState state;
    std::mt19937 rng(42);

    auto printState = [&](const std::string& label) {
        std::cout << label << std::endl;
        std::cout << "  Co:" << state.params.getCohesion()
                  << " Cy:" << state.params.getCybernationLevel()
                  << " HR:" << state.params.getHumanRelation()
                  << " Tech:" << state.params.getTechnology()
                  << " Env:" << state.params.getEnvironment() << std::endl;
        std::cout << "  Board -> ";
        for (int t = 0; t < GameState::NUM_TILE; t++) {
            StackType type = state.board[t].getEffectiveType();
            switch (type) {
                case StackType::WILD:  std::cout << "W "; break;
                case StackType::WASTE: std::cout << "X "; break;
                case StackType::DEV_A: std::cout << "A "; break;
                case StackType::DEV_B: std::cout << "B "; break;
                default:               std::cout << "? "; break;
            }
        }
        std::cout << std::endl;
    };

    auto randomCancelTiles = [&](const std::vector<int>& tiles) -> std::string {
        if (tiles.empty()) return "";

        int cancelCount = std::min((int)tiles.size(), (int)(rng() % 3)); // 0, 1, or 2
        std::string result;
        for (int i = 0; i < cancelCount; i++) {
            if (!result.empty()) result += ",";
            result += std::to_string(tiles[i]);
        }
        return result;
    };

    printState("=== Initial State ===");
    std::cout << std::endl;

    for (int i = 0; i < 10; i++) {
        std::cout << "=== Round " << (i + 1) << " ===" << std::endl;

        ActionResult draw = GameUtility::drawDisruption(state);
        if (!draw.ok()) {
            std::cout << "  Draw failed: " << draw.message.payload << std::endl;
            continue;
        }

        const DisruptionCard& card = state.activeDisruption.value();
        std::string category = card.getCategory();
        std::cout << "  Card: " << card.getName()
                  << " [" << category << "]" << std::endl;
        std::cout << "  Desc: " << card.getDescription() << std::endl;
        std::cout << "  Targets: ";
        for (auto t : card.getStackTargets())
            std::cout << t << " ";
        std::cout << std::endl;


        Action action;
        action.playerId = 0;
        action.type = "resolve_disruption";

        // If the card has stack targets, generate cancel tiles
        if (!card.getStackTargets().empty()) {
            std::string cancel = randomCancelTiles(card.getStackTargets());
            if (!cancel.empty()) {
                action.params["canceltiles"] = cancel;
                std::cout << "  Cancel tiles: " << cancel << std::endl;
            } else {
                std::cout << "  Cancel tiles: (none)" << std::endl;
            }
        }

        ActionResult result = GameUtility::applyDisruptionEffect(state, action);
        std::cout << "  Result: " << (result.ok() ? "SUCCESS" : "FAILED")
                  << " - " << result.message.payload << std::endl;
        printState("  After:");

        state.activeDisruption = std::nullopt;
        std::cout << std::endl;
    }

    std::cout << "=== All disruption tests complete ===" << std::endl;
}


int main(void)
{
    // testLoadStack();
    // testLoadTile();
    // testBoard();
    // testWalkPath();
    // testLoadGoal();
    testDraw10AndResolve();
    return 0;
}