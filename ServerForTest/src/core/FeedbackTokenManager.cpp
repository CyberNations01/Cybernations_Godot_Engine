#include "core/FeedbackTokenManager.hpp"
#include <algorithm>
#include <random>
#include "core/FeedbackPool.hpp"
#include "core/Tile.hpp"

namespace {
TokenEffect tokenFromStackType(StackType type) {
    switch (type) {
        case StackType::WILD:  return TokenEffect::TURN_WILD;
        case StackType::WASTE: return TokenEffect::LOSE_COHESION;
        case StackType::DEV_A: return TokenEffect::TURN_WASTE;
        case StackType::DEV_B: return TokenEffect::SOLVE_DISRUPTION;
        case StackType::UNKNOWN:
        default: return TokenEffect::UNKNOWN;
    }
}
}

void FeedbackTokenManager::clearBag() {
    bag.clear();
}

void FeedbackTokenManager::clearTrack() {
    track.clear();
}

void FeedbackTokenManager::clearAll() {
    clearBag();
    clearTrack();
}

void FeedbackTokenManager::setBag(const std::vector<TokenEffect>& nextBag) {
    bag = nextBag;
}

void FeedbackTokenManager::setTrack(const std::vector<TokenEffect>& nextTrack) {
    track = nextTrack;
}

void FeedbackTokenManager::shuffleBag() {
    std::random_device rd;
    std::mt19937 g(rd());
    std::shuffle(bag.begin(), bag.end(), g);
}

int FeedbackTokenManager::rebuildBagFromBoard(const std::vector<Tile>& board, FeedbackPool& pool) {
    bag.clear();
    for (const auto& tile : board) {
        TokenEffect token = tokenFromStackType(tile.getEffectiveType());
        if (token == TokenEffect::UNKNOWN) continue;
        if (pool.draw(token)) {
            bag.push_back(token);
        }
    }
    return static_cast<int>(bag.size());
}

bool FeedbackTokenManager::drawTrackFromBag(int trackSize) {
    if (bag.empty() || trackSize <= 0) return false;

    shuffleBag();
    const int drawCount = std::min(trackSize, static_cast<int>(bag.size()));
    track.assign(bag.begin(), bag.begin() + drawCount);
    bag.erase(bag.begin(), bag.begin() + drawCount);
    return true;
}
