#include "core/Stack.hpp"

int Stack::getConnectedSide(int side)
{
    if (paths.find(side) == paths.end())
        return -1;
    return paths[side];
}