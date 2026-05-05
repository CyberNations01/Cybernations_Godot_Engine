#ifndef STACK_HPP
#define STACK_HPP
#include <string>
#include "Types.hpp"
#include <vector>
#include <map>

class Stack {
private:
    int id;
    StackType type;
    std::vector<std::vector<std::string>> sides;
    std::map<int, int> paths;

public:
    Stack()  = default;
    ~Stack() = default;

    const StackType&                              getType()  const {return type;};
    const int &                                   getId()    const {return id;};
    const std::vector<std::vector<std::string>>&  getSides() const{return sides;};
    const std::map<int, int>&                     getPaths() const {return paths;};

    void setId(const int& id) {this->id = id;};
    void setPaths(const std::map<int, int>& paths) {this->paths = paths;};
    void setSides(const std::vector<std::vector<std::string>>& sides) {this->sides = sides;};
    void setType(const StackType t)  { type = t; }

    int getConnectedSide(int side);
};

#endif
