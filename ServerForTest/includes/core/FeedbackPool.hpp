#ifndef FEEDBACK_POOL_HPP
#define FEEDBACK_POOL_HPP
#include "Types.hpp"

class FeedbackPool {
private:
    int turnWildTokens;
    int loseCohesionTokens;
    int turnWasteTokens;
    int solveDisruptTokens;
    int developTokens;
    int transformTokens;

public:
    FeedbackPool(int each = 10);
    FeedbackPool(int wild, int loseCoh, int waste, int solve, int develop, int transform);
    ~FeedbackPool() = default;

    // Getters
    int getTurnWild()       const { return turnWildTokens; }
    int getLoseCohesion()   const { return loseCohesionTokens; }
    int getTurnWaste()      const { return turnWasteTokens; }
    int getSolveDisrupt()   const { return solveDisruptTokens; }
    int getDevelop()        const { return developTokens; }
    int getTransform()      const { return transformTokens; }
    int getPoolSize()       const;
    int getTotalCapacity()  const;

    // Operations
    bool draw(TokenEffect token);     // Remove from pool, returns false if empty
    void putBack(TokenEffect token);  // Return to pool
};

#endif
