#ifndef GAME_STATE_HPP
#define GAME_STATE_HPP

#include "core/Params.hpp"
#include "core/FeedbackPool.hpp"
#include "core/FeedbackTokenManager.hpp"
#include "core/Stack.hpp"
#include "core/Tile.hpp"
#include "core/Player.hpp"
#include "core/DisruptionCard.hpp"
#include "core/GamePhase.hpp"
#include "core/CardManager.hpp"
#include "core/DataLoader.hpp"
#include "nlohmann/json.hpp"
#include <vector>
#include <string>
#include <optional>
#include <set>

/*
 * GameState is a pure data container for the entire game state.
 * 
 * It does NOT contain game logic or validation — that's the job of
 * the RoundController and PhaseHandlers.
 * 
 * Both layers read and write GameState:
 *   - RoundController manages turn/phase/round progression
 *   - PhaseHandlers mutate board, params, tokens, etc.
 */
class GameState {
public:
    static constexpr int NUM_PLAYERS = 5;
    static constexpr int NUM_TILE  = 11;
    static constexpr int FEEDBACK_TRACK_SIZE = 11;

    std::vector<Tile>        board;
    std::vector<TokenEffect> tokenBag;
    Player players[NUM_PLAYERS];
    Params       params;
    FeedbackPool pool;
    FeedbackTokenManager tokenManager;

    CardManager<DisruptionCard> disruptionManager;
    CardManager<Goal>  goalManager;    
    CardManager<Stack> wildStackManager;
    CardManager<Stack> wasteStackManager;
    CardManager<Stack> devAStackManager;
    CardManager<Stack> devBStackManager;


    std::pair<int, int> peopleToken;
    bool      ignoreCohesionLossThisRound = false;
    Goal      currentGoal;
    
    std::optional<DisruptionCard> activeDisruption = std::nullopt;
    std::vector<TokenEffect> adaptTrack;
    int adaptCursor = 0;

    GameState();

    const std::pair<int, int> & getPeopleToken() const {return this->peopleToken;};
    const std::vector<Tile>& getBoard() const { return board; }
    Tile* getTile(int position);
    Player* getPlayer(int id);

    bool setPeopleToken(const std::pair<int, int>& pos);
    int  findFirstPlayer() const;
    void rebuildTokenBag();
    void syncTokenBagFromManager();
    void setTokenBag(const std::vector<TokenEffect>& nextBag);
    bool isActiveGoalMet() const;
    void randomizeBoard();
    
    nlohmann::json toJson() const;
    std::string    snapshot() const;

};

#endif
