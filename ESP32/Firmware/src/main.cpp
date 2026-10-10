#include <Arduino.h>
#include <ArduinoJson.h>

String currentPokemon = "Waiting";
int currentLevel = 0;
int currentHP = 0;
String currentStatus = "Idle";

void handlePacket(String packet)
{
    StaticJsonDocument<256> doc;

    DeserializationError error =
        deserializeJson(doc, packet);

    if (error)
        return;

    currentPokemon = doc["Pokemon"] | "Unknown";
    currentLevel = doc["Level"] | 0;
    currentHP = doc["HP"] | 0;
    currentStatus = doc["Status"] | "Normal";
}

void setup()
{
    Serial.begin(115200);

    // OLED initialization will be connected here.
}

void loop()
{
    if (Serial.available())
    {
        String packet =
            Serial.readStringUntil('\n');

        handlePacket(packet);
    }

    // OLED render loop will be added here.

    delay(50);
}
