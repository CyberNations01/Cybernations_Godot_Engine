#ifndef GOAL_HPP
#define GOAL_HPP

#include <string>
#include <vector>
#include <map>
#include "Types.hpp"
#include <optional>

/*
 * victory_conditiion 
 * 
 * Single victory condition to achieve
 * 
 * @type: Target Resoruce i.e. Wild, DevA, Cy, Co
 * @op: Operend
 * @num: Number of required type
 * @position: Specific resource requirement, optional 
*/
struct victory_condition {
    std::string type;
    comparator op;
    int num;
    std::optional<std::string> position;
};

class Goal {
    private:
        int id;
        int reverseGoalId;
        std::string name;
        std::vector<victory_condition> conditions;
        std::map<StackType, std::vector<int>> stackEffect;

    public:
        const int& getId() const            {return id;};
        const int& getReverseGoalId() const {return reverseGoalId;};
        const std::string& getName() const  {return name;};

        const std::vector<victory_condition>& getConditions() const
        {return conditions;};
        const std::map<StackType, std::vector<int>>& getStackEffect() const
        {return stackEffect;};


        void setId(const int& id)                      {this->id = id;};
        void setReverseGoalId(const int& id)           {this->reverseGoalId = id;};
        void setName(const std::string& name)          {this->name = name;};
        void addCondition(const victory_condition& vc) {conditions.push_back(vc);};

        void setStackEffect(const std::map<StackType, std::vector<int>>& stackEffect)
        {this->stackEffect = stackEffect;}
        void setCondition(std::vector<victory_condition> vc)
        {this->conditions = vc;}

};

#endif
