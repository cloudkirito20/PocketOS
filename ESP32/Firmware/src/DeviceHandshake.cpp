#include "DeviceHandshake.h"

void DeviceHandshake::sendHello()
{
    Serial.println("{\"type\":\"HELLO\",\"device\":\"PocketOS ESP32\"}");
}

void DeviceHandshake::sendStatus()
{
    Serial.println("{\"type\":\"STATUS\",\"firmware\":\"3.4.0\",\"ready\":true}");
}
