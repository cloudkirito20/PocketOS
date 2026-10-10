#pragma once

#include <Arduino.h>

class DisplayRenderer
{
public:

    void begin();

    void showPokemon(
        String name,
        int level,
        int hp,
        String status);
};
