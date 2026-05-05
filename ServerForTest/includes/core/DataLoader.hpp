#ifndef DATA_LOADER_HPP
#define DATA_LOADER_HPP
#include "nlohmann/json.hpp"
#include "CardManager.hpp"
#include "DisruptionCard.hpp"
#include "Tile.hpp"
#include "Stack.hpp"
#include "Types.hpp"
#include "Goal.hpp"
#include <fstream>
#include <iostream>

class DataLoader {
    public:
    
        template <typename K>
        static K parseJson(const nlohmann::json &data);
        
        template <typename T>
        static CardManager<T> loadDeck(const std::string & filename);

        static std::vector<DisruptionCard>     loadDisrupt(const std::string & filename);
        static std::vector<Stack>              loadStack(const std::string & filename);
        static std::vector<Tile>               loadTile(const std::string & filename);
        static std::vector<Goal>               loadGoal(const std::string& filename);
};


template <>
CardManager<DisruptionCard> DataLoader::loadDeck<DisruptionCard>(const std::string& filename);

template <>
CardManager<Stack> DataLoader::loadDeck<Stack>(const std::string& filename);

template <>
CardManager<Tile> DataLoader::loadDeck<Tile>(const std::string& filename);

template <>
CardManager<Goal> DataLoader::loadDeck<Goal>(const std::string& filename);





template <>
DisruptionCard DataLoader::parseJson<DisruptionCard>(const nlohmann::json &data);

template <>
Stack DataLoader::parseJson<Stack>(const nlohmann::json &data);

template<>
Tile DataLoader::parseJson<Tile>(const nlohmann::json& data);

template<>
Goal DataLoader::parseJson<Goal>(const nlohmann::json& data);



#endif
