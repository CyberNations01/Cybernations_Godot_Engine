#ifndef DISRUPTION_CARD_HPP
#define DISRUPTION_CARD_HPP

#include <string>
#include <vector>
#include <map>
#include <optional>
#include "Types.hpp"

enum class ConditionType { NONE, STACK, RESOURCE };

// Stack condition: check tile effective type
struct StackCondition {
    std::vector<StackType> stackTypes;  // e.g. {DevA, DevB}
};

// Resource condition: compare two params
struct ResourceCondition {
    CyberParameter lhs;
    CyberParameter rhs;
    comparator compare;  
};

class DisruptionCard {
private:
    std::string name;
    std::string description;
    std::string category;       

    // Condition
    ConditionType conditionType;      // NONE, STACK, RESOURCE
    std::optional<StackCondition> stackCondition;
    std::optional<ResourceCondition> resourceCondition;

    // Targets & effects
    std::vector<int> stackTargets;
    std::vector<std::pair<DisruptionEffect, int>> effects;   // ordered
    std::vector<std::pair<DisruptionEffect, int>> costs;     // ordered

    // Optional bonus action
    std::vector<std::pair<DisruptionEffect, int>> optionalCosts;
    std::vector<std::pair<DisruptionEffect, int>> optionalGains;

    bool cancellable;

public:
    DisruptionCard() : conditionType(ConditionType::NONE),
                       cancellable(false) {}
    ~DisruptionCard() = default;

    // Getters
    const std::string&      getName()          const { return name; }
    const std::string&      getDescription()   const { return description; }
    const std::string&      getCategory()   const { return category; }


    ConditionType           getConditionType() const { return conditionType; }
    bool                    isCancellable()    const { return cancellable; }
    bool                    hasOptional()      const { return !optionalCosts.empty(); }

    const std::vector<int>&                              getStackTargets()  const { return stackTargets; }
    const std::vector<std::pair<DisruptionEffect, int>>& getEffects()       const { return effects; }
    const std::vector<std::pair<DisruptionEffect, int>>& getCosts()         const { return costs; }
    const std::vector<std::pair<DisruptionEffect, int>>& getOptionalCosts() const { return optionalCosts; }
    const std::vector<std::pair<DisruptionEffect, int>>& getOptionalGains() const { return optionalGains; }

    const std::optional<StackCondition>&    getStackCondition()    const { return stackCondition; }
    const std::optional<ResourceCondition>& getResourceCondition() const { return resourceCondition; }

    // Setters
    void setName(const std::string& n)              { name = n; }
    void setDescription(const std::string& d)       { description = d; }
    void setCategory(const std::string& c)          { category = c; }


    void setConditionType(ConditionType ct)          { conditionType = ct; }
    void setCancellable(bool c)                      { cancellable = c; }
    void setStackTargets(const std::vector<int>& t)  { stackTargets = t; }

    void setEffects(const std::vector<std::pair<DisruptionEffect, int>>& e)       { effects = e; }
    void setCosts(const std::vector<std::pair<DisruptionEffect, int>>& c)         { costs = c; }
    void setOptionalCosts(const std::vector<std::pair<DisruptionEffect, int>>& c) { optionalCosts = c; }
    void setOptionalGains(const std::vector<std::pair<DisruptionEffect, int>>& g) { optionalGains = g; }
    void setStackCondition(const StackCondition& sc)       { stackCondition = sc; }
    void setResourceCondition(const ResourceCondition& rc) { resourceCondition = rc; }

    // Helpers
    bool hasTileChangeEffect() const;
};

#endif