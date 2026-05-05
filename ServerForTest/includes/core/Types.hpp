#ifndef TYPES_HPP
#define TYPES_HPP
#include <string>
#include <optional>

enum class StackType {
    WILD,
    WASTE,
    DEV_A,
    DEV_B,
    UNKNOWN
};

enum class CyberParameter {
    COHESION,
    CYBERNATION_LEVEL,
    HUMAN_RELATION,
    ENVIRONMENT,
    TECHNOLOGY
};

enum class TokenEffect {
    TURN_WILD,
    LOSE_COHESION,
    TURN_WASTE,
    SOLVE_DISRUPTION,
    DEVELOP_STACK,
    TRANSFORM_STACK,
    UNKNOWN
};

enum class GamePhase {
    ENVISION,
    TRAVERSE,
    ADOPT,
};

enum class DisruptionEffect {
    // Stack changes
    TURN_WASTE,
    TURN_WILD,
    TURN_DEV_A,
    TURN_DEV_B,

    // Resource changes
    COHESION,
    HUMAN_RELATION,
    CYBERNATION,
    TECHNOLOGY,
    ENVIRONMENT,
    
    RESOURCES,
    TOKEN,
    TRADE,

    // Rule modifiers
    CAP_ENV,
    IGNORE_COHESION_EFFECT,

    // Meta
    SWAP_GOAL,
    DRAW_GOAL,
    MOVE_PEOPLE
};

enum class ActionStatus {
    SUCCESS,            // Action executed successfully
    INVALID_TARGET,            
    INVALID_ACTION,

    INSUFFICIENT_RESOURCE, // Not enough resources to perform action
    NOT_YOUR_TURN,      // Silently ignored by Round Controller
    PLAYER_ALREADY_PASSED, // Player has already passed this phase
    GAME_OVER,          // Game has ended
    UNKNOWN_ERROR
};

enum class comparator {
    GT, GE, EQ, LE, LT, NE
};

inline bool parseCyberParameter(const std::string& raw, CyberParameter& out)
{
    if (raw == "Co" || raw == "CO" || raw == "COHESION") {
        out = CyberParameter::COHESION; return true;
    }
    if (raw == "Cy" || raw == "CY" || raw == "CYBERNATION" || raw == "CYBERNATION_LEVEL") {
        out = CyberParameter::CYBERNATION_LEVEL; return true;
    }
    if (raw == "HR" || raw == "HUMAN_RELATION") {
        out = CyberParameter::HUMAN_RELATION; return true;
    }
    if (raw == "Env" || raw == "ENV" || raw == "ENVIRONMENT") {
        out = CyberParameter::ENVIRONMENT; return true;
    }
    if (raw == "Tech" || raw == "TECH" || raw == "TECHNOLOGY") {
        out = CyberParameter::TECHNOLOGY; return true;
    }
    return false;
}

inline std::string cyberParameterToLabel(const CyberParameter p) {
    switch (p) {
        case CyberParameter::COHESION: return "COHESION";
        case CyberParameter::CYBERNATION_LEVEL: return "CYBERNATION_LEVEL";
        case CyberParameter::HUMAN_RELATION: return "HUMAN_RELATION";
        case CyberParameter::ENVIRONMENT: return "ENVIRONMENT";
        case CyberParameter::TECHNOLOGY: return "TECHNOLOGY";
        default: return "UNKNOWN";
    }
}

inline StackType strToStackType(const std::string& str)
{
    if (str == "Wild")
        return StackType::WILD;
    else if (str == "Waste")
        return StackType::WASTE;
    else if (str == "DevA")
        return StackType::DEV_A;
    else if (str == "DevB")
        return StackType::DEV_B;
    return StackType::UNKNOWN;
}

inline std::string stackTypeToStr(const StackType &t) {
    switch (t) {
        case StackType::WILD:    return "Wild";
        case StackType::WASTE:   return "Waste";
        case StackType::DEV_A:   return "DevA";
        case StackType::DEV_B:   return "DevB";
        default:                 return "Unknown";
    }
}

inline std::string tokenEffectToStr(const TokenEffect effect) {
    switch (effect) {
        case TokenEffect::TURN_WILD: return "TURN_WILD";
        case TokenEffect::LOSE_COHESION: return "LOSE_COHESION";
        case TokenEffect::TURN_WASTE: return "TURN_WASTE";
        case TokenEffect::SOLVE_DISRUPTION: return "SOLVE_DISRUPTION";
        case TokenEffect::DEVELOP_STACK: return "DEVELOP_STACK";
        case TokenEffect::TRANSFORM_STACK: return "TRANSFORM_STACK";
        case TokenEffect::UNKNOWN:
        default: return "UNKNOWN";
    }
}

inline TokenEffect strToTokenEffect(const std::string& str) {
    if (str == "TURN_WILD") return TokenEffect::TURN_WILD;
    if (str == "LOSE_COHESION") return TokenEffect::LOSE_COHESION;
    if (str == "TURN_WASTE") return TokenEffect::TURN_WASTE;
    if (str == "SOLVE_DISRUPTION") return TokenEffect::SOLVE_DISRUPTION;
    if (str == "DEVELOP_STACK") return TokenEffect::DEVELOP_STACK;
    if (str == "TRANSFORM_STACK") return TokenEffect::TRANSFORM_STACK;
    return TokenEffect::UNKNOWN;
}

inline std::string gamePhaseToStr(const GamePhase phase) {
    switch (phase) {
        case GamePhase::ENVISION: return "ENVISION";
        case GamePhase::TRAVERSE: return "TRAVERSE";
        case GamePhase::ADOPT: return "ADOPT";
        default: return "UNKNOWN";
    }
}

inline GamePhase strToGamePhase(const std::string& str) {
    if (str == "ENVISION") return GamePhase::ENVISION;
    if (str == "TRAVERSE") return GamePhase::TRAVERSE;
    if (str == "ADOPT") return GamePhase::ADOPT;
    return GamePhase::ENVISION;
}

inline DisruptionEffect strToDisruptionEffect(const std::string &str)
{
    if (str == "TurnWaste")  return DisruptionEffect::TURN_WASTE;
    if (str == "TurnWild")   return DisruptionEffect::TURN_WILD;
    if (str == "TurnDevA")   return DisruptionEffect::TURN_DEV_A;
    if (str == "TurnDevB")   return DisruptionEffect::TURN_DEV_B;
    if (str == "Co")         return DisruptionEffect::COHESION;
    if (str == "HR")         return DisruptionEffect::HUMAN_RELATION;
    if (str == "Cy")         return DisruptionEffect::CYBERNATION;
    if (str == "Tech")       return DisruptionEffect::TECHNOLOGY;
    if (str == "Env")        return DisruptionEffect::ENVIRONMENT;
    if (str == "Resources")  return DisruptionEffect::RESOURCES;
    if (str == "Token")      return DisruptionEffect::TOKEN;
    if (str == "Trade")      return DisruptionEffect::TRADE;
    if (str == "CapEnv")     return DisruptionEffect::CAP_ENV;
    if (str == "IgnoreCohesionEffect") return DisruptionEffect::IGNORE_COHESION_EFFECT;
    if (str == "SwapGoal")   return DisruptionEffect::SWAP_GOAL;
    if (str == "DrawGoal")   return DisruptionEffect::DRAW_GOAL;
    if (str == "MovePpl")     return DisruptionEffect::MOVE_PEOPLE;
    return DisruptionEffect::COHESION; // fallback
}

inline CyberParameter disruptionEffectToCyberParameter(const DisruptionEffect& eff)
{
    switch (eff) {
        case DisruptionEffect::CYBERNATION:    
            return CyberParameter::CYBERNATION_LEVEL; 
        case DisruptionEffect::COHESION:
            return CyberParameter::COHESION;
        case DisruptionEffect::HUMAN_RELATION:
            return CyberParameter::HUMAN_RELATION;
        case DisruptionEffect::TECHNOLOGY:
            return CyberParameter::TECHNOLOGY;
        case DisruptionEffect::ENVIRONMENT:
            return CyberParameter::ENVIRONMENT;
        default:
            return CyberParameter::CYBERNATION_LEVEL;
    }
}

inline std::optional<CyberParameter> tryDisruptionEffectToCyberParameter(const DisruptionEffect& eff)
{
    switch (eff) {
        case DisruptionEffect::CYBERNATION:
            return CyberParameter::CYBERNATION_LEVEL;
        case DisruptionEffect::COHESION:
            return CyberParameter::COHESION;
        case DisruptionEffect::HUMAN_RELATION:
            return CyberParameter::HUMAN_RELATION;
        case DisruptionEffect::TECHNOLOGY:
            return CyberParameter::TECHNOLOGY;
        case DisruptionEffect::ENVIRONMENT:
            return CyberParameter::ENVIRONMENT;
        default:
            return std::nullopt;
    }
}

inline comparator strToComparator(const std::string& str)
{
    if (str == "GT") return comparator::GT;
    if (str == "GE") return comparator::GE;
    if (str == "EQ") return comparator::EQ;
    if (str == "LE") return comparator::LE;
    if (str == "LT") return comparator::LT;
    if (str == "NE") return comparator::NE;
    return comparator::EQ;
}

inline std::string comparatorToStr(const comparator& op)
{
    switch (op) {
        case comparator::GT: return "GT";
        case comparator::GE: return "GE";
        case comparator::EQ: return "EQ";
        case comparator::LE: return "LE";
        case comparator::LT: return "LT";
        case comparator::NE: return "NE";
        default:             return "EQ";
    }
}

inline TokenEffect mapStackTypeToFeedbackToken(StackType type) {
    switch(type){
        case StackType::WILD:
            return TokenEffect::TURN_WILD;
        case StackType::WASTE:
            return TokenEffect::LOSE_COHESION;
        case StackType::DEV_A: // Works
            return TokenEffect::TURN_WASTE;
        case StackType::DEV_B: // Agora
            return TokenEffect::SOLVE_DISRUPTION;
        default:
            return TokenEffect::UNKNOWN;
    }
}

inline bool compareWithOp(int lhs, comparator op, int rhs) {
    switch (op) {
        case comparator::GT: return lhs > rhs;
        case comparator::GE: return lhs >= rhs;
        case comparator::EQ: return lhs == rhs;
        case comparator::LE: return lhs <= rhs;
        case comparator::LT: return lhs < rhs;
        case comparator::NE: return lhs != rhs;
        default: return false;
    }
}

inline std::string actionStatusToStr(ActionStatus status) {
    switch (status) {
        case ActionStatus::SUCCESS:
            return "success";
        case ActionStatus::INVALID_TARGET:
            return "Invalid Target";
        case ActionStatus::INVALID_ACTION:
            return "Invalid Action";
        case ActionStatus::UNKNOWN_ERROR:
            return "Unknown Error";
        default:
            return "Invalid";   
    };

    return "";
}
#endif