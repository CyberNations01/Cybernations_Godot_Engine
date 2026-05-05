#ifndef PARAMS_HPP
#define PARAMS_HPP
#include "Types.hpp"

class Params {
private:
    static constexpr int MAX_PARAMS_LEVEL = 25; 
    int cohesion         = 10;
    int cybernationLevel = 2;
    int humanRelation    = 7;
    int environment      = 7;
    int technology       = 7;

public:
    Params() = default;
    Params(int cyberLevel, int humanRel, int env, int tech, int coh);
    ~Params() = default;

    // Getters
    const int& getCohesion()          const { return cohesion; }
    const int& getCybernationLevel()  const { return cybernationLevel; }
    const int& getHumanRelation()     const { return humanRelation; }
    const int& getEnvironment()       const { return environment; }
    const int& getTechnology()        const { return technology; }

    /* Return direct param level Given a CyberParameter */
    int getParamAmount(CyberParameter param) const;

    // Setters (with validation — cohesion caps the others)
    void setCohesion(const int& val);
    void setCybernationLevel(const int& val);
    void setHumanRelation(const int& val);
    void setEnvironment(const int& val);
    void setTechnology(const int& val);

    // Convenience: adjust by delta instead of absolute set
    void adjustParam(CyberParameter param, int delta);


};

#endif
