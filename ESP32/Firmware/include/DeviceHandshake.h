#pragma once

#include <Arduino.h>

class DeviceHandshake
{
public:
    void sendHello();
    void sendStatus();
};
