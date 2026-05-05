#include "core/DisruptionCard.hpp"
#include <fstream>
#include <iostream>
#include <algorithm>
#include <random>

bool DisruptionCard::hasTileChangeEffect() const {
    for (const auto& eff : effects) {
        if (eff.first == DisruptionEffect::TURN_WASTE ||
            eff.first == DisruptionEffect::TURN_WILD  ||
            eff.first == DisruptionEffect::TURN_DEV_A ||
            eff.first == DisruptionEffect::TURN_DEV_B) {
            return true;
        }
    }
    return false;
}