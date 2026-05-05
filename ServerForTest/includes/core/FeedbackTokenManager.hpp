#ifndef FEEDBACK_TOKEN_MANAGER_HPP
#define FEEDBACK_TOKEN_MANAGER_HPP

#include <vector>
#include "Types.hpp"

class Tile;
class FeedbackPool;

class FeedbackTokenManager {
private:
    std::vector<TokenEffect> bag;
    std::vector<TokenEffect> track;

public:
    FeedbackTokenManager() = default;

    void clearBag();
    void clearTrack();
    void clearAll();

    void setBag(const std::vector<TokenEffect>& nextBag);
    void setTrack(const std::vector<TokenEffect>& nextTrack);

    const std::vector<TokenEffect>& getBag() const { return bag; }
    const std::vector<TokenEffect>& getTrack() const { return track; }

    void shuffleBag();

    // Envision step 1: generate bag from board state with finite reserve.
    int rebuildBagFromBoard(const std::vector<Tile>& board, FeedbackPool& pool);

    // Envision step 2: draw to feedback track (inner->outer slots).
    bool drawTrackFromBag(int trackSize);
};

#endif
