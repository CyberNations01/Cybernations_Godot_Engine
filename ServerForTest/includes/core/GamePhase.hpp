#ifndef GAME_PHASE_HPP
#define GAME_PHASE_HPP
#include "Types.hpp"

// Total number of phases per round
constexpr int NUM_PHASES = 3;

// Get next phase, wraps around (Adopt -> Envision = new round)
inline GamePhase nextPhase(GamePhase current) {
    int next = (static_cast<int>(current) + 1) % NUM_PHASES;
    return static_cast<GamePhase>(next);
}

#endif
