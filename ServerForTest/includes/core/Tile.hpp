#ifndef TILE_HPP
#define TILE_HPP
#include "Stack.hpp"
#include <map>
#include <vector>
#include "Types.hpp"
#include <optional>

class Tile {
    private:
        int position;
        int rotation;
        TokenEffect token;

        Stack base;
        std::optional<Stack> overlay;

        // The neighbor Tile from each side
        // { Tile , side }
        std::vector<std::pair<int,int>> neighbours;
    
    public:
        static constexpr int TILE_SIDES = 6;

        Tile(int position, int rotation, std::vector<int> neighbour);
        Tile() = default;
        ~Tile() = default;

        const int&               getRotation()      const {return rotation;};
        const int&               getPosition()      const {return position;};
        const Stack&             getOverlay()       const {return overlay.value();};
        const Stack&             getBase()          const {return base;};
        const TokenEffect&       getToken()         const {return token;};

        const StackType&         getEffectiveType() const;
        const int&                                     getNeighbourTile(const int& side) const;
        const int&                                     getNeighbourSide(int side) const;
        const std::vector<std::pair<int,int>>&         getNeighbours() const { return neighbours; }
        const std::vector<std::string>                 getSideResources(int side)  const;
        int                                            boardSideToStackSide(int boardSide) const;
        int                                            stackSideToBoardSide(int stackSide) const;

        void    setNeighbour(const std::vector<std::pair<int,int>>& nTile) {neighbours = nTile;};
        void    setToken(const TokenEffect &token) {this->token = token;};
        void    setRotation(const int& rotation) {this->rotation = rotation;};
        void    setBase(const Stack& base) {this->base = base;};
        void    setOverlay(const Stack& s) {overlay = s;};
        void    setPosition(int pos) {position = pos;};
        
        bool    hasOverlay() const {return overlay.has_value();}
        std::optional<Stack> removeOverlay();

        // ! TODO
        void    flip();
};

#endif