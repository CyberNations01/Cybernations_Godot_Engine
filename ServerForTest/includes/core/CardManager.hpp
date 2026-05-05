#ifndef CARD_MANAGER_HPP
#define CARD_MANAGER_HPP
#include <vector>
#include <algorithm>
#include <random>
#include <iostream>

template <typename T>
class CardManager {
private:
    std::vector<T> deck;
    std::vector<T> discard;  

public:
    CardManager() = default;
    CardManager(const std::vector<T> &deck) : deck(deck) {}

    T draw() {
        if (deck.empty()) {
            reshuffle();
            if (deck.empty()) {
                std::cerr << "Deck and discard are both empty" << std::endl;
                return T();
            }
        }
        T card = deck.front();
        deck.erase(deck.begin());
        discard.push_back(card);
        return card;
    }

    void shuffle() {
        std::random_device rd;
        std::mt19937 g(rd());
        std::shuffle(deck.begin(), deck.end(), g);
    }

    void returnToDiscard(const T& card) {
        discard.push_back(card);
    }

    void reshuffle() {
        for (auto& card : discard)
            deck.push_back(card);
        discard.clear();
        shuffle();
    }

    bool isDeckEmpty()    const { return deck.empty(); }
    int  getDeckSize()    const { return static_cast<int>(deck.size()); }
    int  getDiscardSize() const { return static_cast<int>(discard.size()); }
    const std::vector<T>& getDeck() const { return deck; };
    const std::vector<T>& getDiscard() const { return discard; };
};

#endif