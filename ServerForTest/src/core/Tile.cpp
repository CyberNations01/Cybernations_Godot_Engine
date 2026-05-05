#include "core/Tile.hpp"

namespace {
int normalizeSide(int side)
{
    side %= Tile::TILE_SIDES;
    if (side < 0) side += Tile::TILE_SIDES;
    return side;
}
}

const StackType &Tile::getEffectiveType() const
{
    if (hasOverlay()) return overlay->getType();
    return base.getType();
}

const int &Tile::getNeighbourTile(const int &side) const
{
    return neighbours[side].first;
}

const int &Tile::getNeighbourSide(int side) const
{
    return neighbours[side].second;
}

const std::vector<std::string> Tile::getSideResources(int side) const
{
    const int stackSide = boardSideToStackSide(side);
    std::vector<std::string> all_resources = base.getSides()[stackSide];
    if (hasOverlay()) {
        all_resources.insert(all_resources.end(), 
                             overlay->getSides()[stackSide].begin(),
                             overlay->getSides()[stackSide].end());
    }
    return all_resources;
}

int Tile::boardSideToStackSide(int boardSide) const
{
    return normalizeSide(boardSide - rotation);
}

int Tile::stackSideToBoardSide(int stackSide) const
{
    return normalizeSide(stackSide + rotation);
}

std::optional<Stack> Tile::removeOverlay()
{
    if (!overlay.has_value()) return std::nullopt;
    Stack removed = overlay.value();
    overlay.reset();
    return removed;
}