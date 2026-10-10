// Pokemon Partner OLED - Custom-type-free build 2026-10-07
#include <WiFi.h>
#include <WebServer.h>
#include <Wire.h>
#include <Adafruit_GFX.h>
#include <Adafruit_SSD1306.h>
#include <ArduinoJson.h>
#include <Preferences.h>

// Pokemon Partner - ESP32 + SSD1306 128x64
// Keep your existing Wi-Fi credentials here.
const char* WIFI_SSID = "YOUR_WIFI_NAME";
const char* WIFI_PASS = "YOUR_WIFI_PASSWORD";

#define SCREEN_WIDTH 128
#define SCREEN_HEIGHT 64
#define OLED_RESET -1
#define OLED_ADDR 0x3C

Adafruit_SSD1306 display(SCREEN_WIDTH, SCREEN_HEIGHT, &Wire, OLED_RESET);
WebServer server(80);
Preferences pocketPrefs;
String serialLine;
void startPocketWifiOnce();
static bool pocketWifiStarted = false;
// Wi-Fi is OFF at boot for stability diagnosis; activate via WIFI_START, WIFI_SCAN, or WIFI_JOIN.
unsigned long lastWifiRetryMs = 0;
const unsigned long WIFI_RETRY_MS = 15000;
bool pocketMenuVisible = false;
int pocketActiveApp = 0; // 0 = Partner; 1 = Info, 2 = Encounter, 3.. = utilities
int pocketMenuIndex = 0;
const char* pocketApps[] = {"POKEMON PARTNER", "POKEMON INFO", "WILD ENCOUNTER", "SHINY HUNT", "CLOCK", "ABOUT", "WI-FI TOOLS", "PC MONITOR", "SETTINGS"};
const int pocketAppCount = sizeof(pocketApps) / sizeof(pocketApps[0]);


// Pixel-art sprites for boot and idle animations.
// SPR_PIKA_0: 24x24
const uint8_t PROGMEM SPR_PIKA_0[] = {
  0x30, 0x00, 0x0C, 0x78, 0x00, 0x1E, 0x78, 0x00, 0x1E, 0x38, 0x00, 0x1C,
  0x38, 0x00, 0x1C, 0x1C, 0x00, 0x38, 0x1F, 0xFF, 0xF8, 0x3F, 0xFF, 0xFC,
  0x7F, 0xFF, 0xFE, 0x7F, 0x3F, 0x3F, 0x7F, 0x3F, 0x3F, 0x7F, 0xFF, 0xFE,
  0x7D, 0xFF, 0xBE, 0x78, 0xFF, 0x1E, 0x3F, 0xFF, 0xFC, 0x1F, 0xFF, 0xF8,
  0x07, 0xFF, 0xE0, 0x03, 0xFF, 0xC0, 0x07, 0xFF, 0xE0, 0x0F, 0xCF, 0xC0,
  0x1F, 0x0F, 0x98, 0x3E, 0x07, 0xDC, 0x1C, 0x03, 0x98, 0x08, 0x01, 0x00,
};

// SPR_PIKA_1: 24x24
const uint8_t PROGMEM SPR_PIKA_1[] = {
  0x60, 0x00, 0x06, 0xF0, 0x00, 0x0F, 0x78, 0x00, 0x1E, 0x38, 0x00, 0x1C,
  0x38, 0x00, 0x1C, 0x1C, 0x00, 0x38, 0x1F, 0xFF, 0xF8, 0x3F, 0xFF, 0xFC,
  0x7F, 0xFF, 0xFE, 0x7F, 0x3F, 0x3F, 0x7F, 0x3F, 0x3F, 0x7F, 0xFF, 0xFE,
  0x7D, 0xFF, 0xBE, 0x78, 0xFF, 0x1E, 0x3F, 0xFF, 0xFC, 0x1F, 0xFF, 0xF8,
  0x07, 0xFF, 0xE0, 0x03, 0xFF, 0xC0, 0x07, 0xFF, 0xE0, 0x0F, 0xCF, 0xC0,
  0x3E, 0x1F, 0x1C, 0x1E, 0x1E, 0x38, 0x0E, 0x1C, 0x18, 0x04, 0x08, 0x00,
};

// SPR_TRAINER_W0: 16x20
const uint8_t PROGMEM SPR_TRAINER_W0[] = {
  0x0F, 0xC0, 0x1F, 0xE0, 0x3F, 0xF0, 0x3B, 0xE0, 0x1F, 0xC0, 0x0F, 0x80,
  0x1F, 0xC0, 0x3F, 0xE0, 0x77, 0xF0, 0x67, 0xE0, 0x07, 0xE0, 0x0F, 0xF0,
  0x0E, 0xF0, 0x0E, 0x70, 0x1C, 0x38, 0x38, 0x1C, 0x30, 0x0C, 0x70, 0x0E,
  0xE0, 0x06, 0xC0, 0x03,
};

// SPR_TRAINER_W1: 16x20
const uint8_t PROGMEM SPR_TRAINER_W1[] = {
  0x0F, 0xC0, 0x1F, 0xE0, 0x3F, 0xF0, 0x3B, 0xE0, 0x1F, 0xC0, 0x0F, 0x80,
  0x1F, 0xC0, 0x3F, 0xE0, 0x77, 0xF0, 0x67, 0xE0, 0x07, 0xE0, 0x0F, 0xF0,
  0x0E, 0xF0, 0x0E, 0x70, 0x06, 0x60, 0x0E, 0x70, 0x1C, 0x38, 0x38, 0x1C,
  0x70, 0x0E, 0xC0, 0x0C,
};

// SPR_TRAINER_W2: 16x20
const uint8_t PROGMEM SPR_TRAINER_W2[] = {
  0x0F, 0xC0, 0x1F, 0xE0, 0x3F, 0xF0, 0x3B, 0xE0, 0x1F, 0xC0, 0x0F, 0x80,
  0x1F, 0xC0, 0x3F, 0xE0, 0x77, 0xF0, 0x67, 0xE0, 0x07, 0xE0, 0x0F, 0xF0,
  0x0E, 0xF0, 0x0E, 0x70, 0x1C, 0x38, 0x38, 0x1C, 0x70, 0x0E, 0x60, 0x06,
  0xC0, 0x03, 0x60, 0x06,
};

// SPR_TRAINER_LOOK_L: 16x20
const uint8_t PROGMEM SPR_TRAINER_LOOK_L[] = {
  0x1F, 0x80, 0x3F, 0xC0, 0x7F, 0xE0, 0x7D, 0xC0, 0x3F, 0x80, 0x1F, 0x00,
  0x3F, 0x80, 0x7F, 0xC0, 0xEF, 0xE0, 0xCF, 0xC0, 0x0F, 0xC0, 0x1F, 0xE0,
  0x1D, 0xE0, 0x1C, 0xE0, 0x1C, 0xE0, 0x1C, 0xE0, 0x38, 0x70, 0x30, 0x30,
  0x70, 0x38, 0x60, 0x18,
};

// SPR_TRAINER_LOOK_R: 16x20
const uint8_t PROGMEM SPR_TRAINER_LOOK_R[] = {
  0x03, 0xF0, 0x07, 0xF8, 0x0F, 0xFC, 0x07, 0x7C, 0x03, 0xF8, 0x01, 0xF0,
  0x03, 0xF8, 0x07, 0xFC, 0x0F, 0xEE, 0x07, 0xE6, 0x07, 0xE0, 0x0F, 0xF0,
  0x0F, 0x70, 0x0E, 0x70, 0x0E, 0x70, 0x0E, 0x70, 0x1C, 0x38, 0x18, 0x18,
  0x38, 0x1C, 0x30, 0x0C,
};

// SPR_GRASS_0: 8x8
const uint8_t PROGMEM SPR_GRASS_0[] = {
  0x00, 0x22, 0x66, 0x28, 0xBA, 0x7C, 0x38, 0xFF,
};

// SPR_GRASS_1: 8x8
const uint8_t PROGMEM SPR_GRASS_1[] = {
  0x00, 0x48, 0x26, 0x14, 0x5D, 0x3E, 0x1C, 0xFF,
};

// SPR_BALL_0: 12x12
const uint8_t PROGMEM SPR_BALL_0[] = {
  0x0F, 0x00, 0x3F, 0xC0, 0x70, 0xE0, 0xE0, 0x70, 0xC0, 0x30, 0xFF, 0xF0,
  0xF0, 0xF0, 0xF6, 0xF0, 0xFF, 0xF0, 0x70, 0xE0, 0x3F, 0xC0, 0x0F, 0x00,
};

// SPR_BALL_1: 12x12
const uint8_t PROGMEM SPR_BALL_1[] = {
  0x0F, 0x00, 0x3F, 0xC0, 0x70, 0xE0, 0xE0, 0x70, 0xC6, 0x30, 0xFF, 0xF0,
  0xE0, 0x70, 0xE6, 0x70, 0xFF, 0xF0, 0x70, 0xE0, 0x3F, 0xC0, 0x0F, 0x00,
};



// Original 1-bit sprites inspired by late-1990s handheld RPG overworlds.
// These are custom sprites, not copied game assets.
// 16x16 trainer, seen from above / slightly behind.
const uint8_t PROGMEM SPR_GBC_TRAINER_DOWN_0[] = {
  0x07,0xE0, 0x0F,0xF0, 0x1F,0xF8, 0x1C,0x38,
  0x1F,0xF8, 0x0F,0xF0, 0x07,0xE0, 0x03,0xC0,
  0x07,0xE0, 0x0F,0xF0, 0x1B,0xD8, 0x13,0xC8,
  0x03,0xC0, 0x07,0xE0, 0x0C,0x30, 0x18,0x18
};

const uint8_t PROGMEM SPR_GBC_TRAINER_DOWN_1[] = {
  0x07,0xE0, 0x0F,0xF0, 0x1F,0xF8, 0x1C,0x38,
  0x1F,0xF8, 0x0F,0xF0, 0x07,0xE0, 0x03,0xC0,
  0x07,0xE0, 0x0F,0xF0, 0x1B,0xD8, 0x13,0xC8,
  0x03,0xC0, 0x07,0xE0, 0x18,0x18, 0x0C,0x30
};

const uint8_t PROGMEM SPR_GBC_TRAINER_LEFT[] = {
  0x03,0xE0, 0x07,0xF0, 0x0F,0xF8, 0x0E,0x70,
  0x0F,0xF0, 0x07,0xE0, 0x03,0xC0, 0x03,0xC0,
  0x07,0xE0, 0x0F,0xF0, 0x17,0xD8, 0x13,0xC8,
  0x03,0xC0, 0x07,0xE0, 0x0C,0x30, 0x18,0x18
};

const uint8_t PROGMEM SPR_GBC_TRAINER_RIGHT[] = {
  0x07,0xC0, 0x0F,0xE0, 0x1F,0xF0, 0x0E,0x70,
  0x0F,0xF0, 0x07,0xE0, 0x03,0xC0, 0x03,0xC0,
  0x07,0xE0, 0x0F,0xF0, 0x1B,0xE8, 0x13,0xC8,
  0x03,0xC0, 0x07,0xE0, 0x0C,0x30, 0x18,0x18
};

// 8x8 tall-grass tiles. Two animation phases and one rustle phase.
const uint8_t PROGMEM SPR_GBC_GRASS_A[] = {
  0x00,0x11,0x52,0x2A,0x95,0x4A,0x24,0x00
};
const uint8_t PROGMEM SPR_GBC_GRASS_B[] = {
  0x00,0x44,0x29,0x52,0x2A,0x91,0x48,0x00
};
const uint8_t PROGMEM SPR_GBC_GRASS_RUSTLE[] = {
  0x24,0x5A,0xA5,0x5A,0xA5,0x5A,0x24,0x00
};



// Extra original sprites for the Yellow-style overworld idle scene.
// These are custom 1-bit sprites inspired by late-1990s handheld RPGs.
const uint8_t PROGMEM SPR_GBC_TRAINER_UP_0[] = {
  0x07,0xE0, 0x0F,0xF0, 0x1F,0xF8, 0x1B,0xD8,
  0x1F,0xF8, 0x0F,0xF0, 0x07,0xE0, 0x03,0xC0,
  0x07,0xE0, 0x0F,0xF0, 0x1B,0xD8, 0x13,0xC8,
  0x03,0xC0, 0x07,0xE0, 0x0C,0x30, 0x18,0x18
};
const uint8_t PROGMEM SPR_GBC_TRAINER_UP_1[] = {
  0x07,0xE0, 0x0F,0xF0, 0x1F,0xF8, 0x1B,0xD8,
  0x1F,0xF8, 0x0F,0xF0, 0x07,0xE0, 0x03,0xC0,
  0x07,0xE0, 0x0F,0xF0, 0x1B,0xD8, 0x13,0xC8,
  0x03,0xC0, 0x07,0xE0, 0x18,0x18, 0x0C,0x30
};

// 8x8 Pikachu-like follower. Original sprite, two-step animation.
const uint8_t PROGMEM SPR_FOLLOWER_0[] = {
  0x42, 0x66, 0x3C, 0x7E, 0xDB, 0x7E, 0x24, 0x42
};
const uint8_t PROGMEM SPR_FOLLOWER_1[] = {
  0x24, 0x66, 0x3C, 0x7E, 0xDB, 0x7E, 0x42, 0x24
};

// 8x8 route edge / shrub tile.
const uint8_t PROGMEM SPR_ROUTE_SHRUB[] = {
  0x3C, 0x7E, 0xDB, 0xFF, 0xA5, 0x7E, 0x3C, 0x18
};

String pokeName = "Waiting...";
String sourceName = "";
String gameName = "";
String natureName = "";
String natureEffect = "";
String abilityName = "";
String genderName = "";
String moveName[4] = {"-", "-", "-", "-"};
int species = 0;
int level = 0;
int stars = 1;
int perfectIVs = 0;
bool shiny = false;
int ivs[6] = {0,0,0,0,0,0}; // HP ATK DEF SPA SPD SPE
int evs[6] = {0,0,0,0,0,0};
int stats[6] = {0,0,0,0,0,0}; // HP ATK DEF SPA SPD SPE

unsigned long lastUpdateMs = 0;
unsigned long pageStartedMs = 0;
unsigned long encounterId = 0;
unsigned long lastIdleFrameMs = 0;
unsigned long lastBridgeMs = 0;
int currentPage = 0;
int idleFrame = 0;
bool bridgeSeen = false;
bool encounterTransitionPending = false;

// Four useful encounter pages. EVs are summarized on the IV page because
// fresh wild Pokemon normally have zero EVs.
const unsigned long PAGE_DURATION_MS = 2800;
const unsigned long ENCOUNTER_TIMEOUT_MS = 6000;
const unsigned long IDLE_FRAME_MS = 120;
const int PAGE_COUNT = 4;

String clipped(String s, int n) {
  if ((int)s.length() <= n) return s;
  return s.substring(0, n);
}

String shortGender() {
  if (genderName == "Male") return "M";
  if (genderName == "Female") return "F";
  if (genderName == "Genderless") return "-";
  return "?";
}

void centerText(const String &s, int y, int size = 1) {
  int16_t x1, y1;
  uint16_t w, h;
  display.setTextSize(size);
  display.getTextBounds(s, 0, y, &x1, &y1, &w, &h);
  display.setCursor((SCREEN_WIDTH - (int)w) / 2, y);
  display.print(s);
}

void drawPixelBall(int x, int y, int frame) {
  const uint8_t* bmp = (frame & 1) ? SPR_BALL_1 : SPR_BALL_0;
  display.drawBitmap(x, y, bmp, 12, 12, SSD1306_WHITE);
}

void drawPixelPikachu(int x, int y, int frame) {
  const uint8_t* bmp = (frame & 1) ? SPR_PIKA_1 : SPR_PIKA_0;
  display.drawBitmap(x, y, bmp, 24, 24, SSD1306_WHITE);
}

// 120 x 48 monochrome conversion of the user-supplied LazyTech logo.
const uint8_t PROGMEM LAZYTECH_MONO_120X48[] = {
  0x00,0x00,0x00,0x00,0x00,0x03,0x80,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
  0x00,0x00,0x00,0x00,0x0F,0x80,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
  0x00,0x00,0x00,0x1F,0x80,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
  0x00,0x00,0x1F,0x80,0x00,0x00,0x06,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
  0x00,0x3F,0x80,0x00,0x00,0x0F,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
  0x3F,0x80,0xFF,0xFF,0xF9,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x3F,
  0x83,0xFF,0xFF,0xFF,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x3F,0x87,
  0xFF,0xFE,0x06,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x3F,0x87,0xFF,
  0xFC,0x00,0xE0,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x3F,0x8F,0xFF,0xFF,
  0xFF,0xA0,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x3F,0x87,0xFF,0xFF,0xFF,
  0xE0,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x3F,0x87,0xFF,0xFF,0xF0,0xC0,
  0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x3F,0x81,0xFF,0xFF,0xC0,0x00,0x00,
  0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x3F,0x80,0x01,0xE0,0x00,0x00,0x00,0x00,
  0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x3F,0x80,0x01,0xF0,0x00,0x00,0x00,0x00,0x00,
  0x00,0x00,0x00,0x00,0x00,0x00,0x3F,0x80,0x01,0xF8,0x00,0x00,0x00,0x00,0x00,0x00,
  0x00,0x00,0x00,0x00,0x00,0x3F,0x80,0x01,0xFC,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
  0x00,0x00,0x00,0x00,0x3F,0x80,0x01,0xFC,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
  0x00,0x00,0x00,0x7F,0x80,0x01,0xFC,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
  0x00,0x00,0x7F,0x80,0x01,0xFC,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
  0x00,0x7F,0x80,0x01,0xFC,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
  0x7F,0xC0,0x01,0xFC,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x7F,
  0xC0,0x01,0xFC,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x3F,0xFF,
  0xF1,0xFC,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x3F,0xFF,0xFC,
  0xFC,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x3F,0xFF,0xFE,0x7C,
  0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x1F,0xFF,0xFF,0x3C,0x00,
  0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x1F,0xFF,0xFF,0x9C,0x00,0x00,
  0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x0F,0xFF,0xFF,0xCC,0x00,0x00,0x00,
  0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x03,0xFF,0xFF,0xC4,0x00,0x00,0x00,0x00,
  0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
  0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
  0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
  0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
  0x00,0x0C,0x00,0x00,0x00,0x00,0x00,0x00,0x04,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
  0x0E,0x00,0x00,0x00,0x07,0xFC,0x00,0x0C,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x0C,
  0x00,0x00,0x00,0x07,0xF8,0x00,0x0C,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x0C,0x03,
  0xE7,0xFE,0x38,0xC3,0xC3,0xCF,0xC0,0x00,0x00,0x00,0x00,0x00,0x00,0x0C,0x07,0xF7,
  0xFE,0x38,0xC7,0xE7,0xEF,0xF0,0x00,0x00,0x00,0x00,0x00,0x00,0x0C,0x06,0x70,0xE7,
  0x70,0xCE,0x66,0x6E,0x70,0x00,0x00,0x00,0x00,0x00,0x00,0x0E,0x03,0xF1,0xC7,0x70,
  0xCF,0xFC,0x0C,0x30,0x00,0x00,0x00,0x00,0x00,0x00,0x0C,0x0F,0xF3,0x83,0xE0,0xCF,
  0xFC,0x0C,0x30,0x00,0x00,0x00,0x00,0x00,0x00,0x0F,0xEE,0x77,0x03,0xE0,0xCE,0x0E,
  0x6C,0x30,0x00,0x00,0x00,0x00,0x00,0x00,0x0F,0xFF,0xFF,0xF1,0xE0,0xC7,0xE7,0xEC,
  0x30,0x00,0x00,0x00,0x00,0x00,0x00,0x07,0xF7,0xFF,0xF1,0xC0,0xC3,0xC3,0xCC,0x30,
  0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x01,0xC0,0x00,0x00,0x00,0x00,0x00,
  0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x0F,0x80,0x00,0x00,0x00,0x00,0x00,0x00,
  0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x07,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
};

void lazyTechReveal() {
  // Open the caught ball, then reveal LazyTech before opening the main menu.
  for (int f=0; f<8; ++f) {
    display.clearDisplay();
    display.drawCircle(64,32,14,SSD1306_WHITE);
    display.drawFastHLine(50,32,28,SSD1306_WHITE);
    display.drawCircle(64,32,4,SSD1306_WHITE);
    int open=f*2;
    display.fillRect(46,16,36,open,SSD1306_BLACK);
    display.drawFastHLine(50,29-open,28,SSD1306_WHITE);
    display.drawFastHLine(50,35+open,28,SSD1306_WHITE);
    display.display(); delay(72);
  }
  for (int f=0; f<5; ++f) {
    display.clearDisplay();
    // Reveal vertical bands for a short retro logo appearance.
    display.drawBitmap(4,8,LAZYTECH_MONO_120X48,120,48,SSD1306_WHITE);
    if (f<4) display.fillRect(4+(f+1)*24,8,120-(f+1)*24,48,SSD1306_BLACK);
    display.display(); delay(110);
  }
  delay(820);
}

void pocketLogoBoot() {
  display.clearDisplay();
  display.setTextSize(2); display.setTextColor(SSD1306_WHITE);
  display.setCursor(10, 17); display.print("PocketOS");
  display.setTextSize(1); display.setCursor(21, 40); display.print("POKEMON EDITION");
  display.drawRect(6, 52, 116, 7, SSD1306_WHITE);
  for (int i=0;i<=110;i+=11) { display.fillRect(9,54,i,3,SSD1306_WHITE); display.display(); delay(55); }
  delay(350);
}
void playBootAnimation() {
  display.setTextColor(SSD1306_WHITE);
  display.setTextWrap(false);

  // 1) Pikachu enters and idles for a moment.
  for (int f = 0; f < 8; f++) {
    display.clearDisplay();
    int bob = (f % 4 == 1 || f % 4 == 2) ? -1 : 0;
    drawPixelPikachu(12, 25 + bob, f / 2);
    display.setTextSize(1);
    centerText("A wild Pikachu!", 4);
    display.display();
    delay(85);
  }

  // 2) Pixel Pokeball arcs toward Pikachu.
  for (int f = 0; f < 18; f++) {
    display.clearDisplay();
    drawPixelPikachu(12, 25, f / 3);
    int ballX = 112 - (f * 4);
    int arc = (int)(14.0 * sin((float)f / 17.0 * PI));
    int ballY = 39 - arc;
    drawPixelBall(ballX, ballY, f / 2);
    centerText("GO!", 4);
    display.display();
    delay(60);
  }

  // 3) Capture flash / beam. Pikachu shrinks into the ball.
  for (int f = 0; f < 7; f++) {
    display.clearDisplay();
    if (f < 3) {
      drawPixelPikachu(12, 25, f);
      for (int i = 0; i < 4; i++) {
        display.drawLine(36, 33 + i * 3, 61, 34 + i * 2, SSD1306_WHITE);
      }
    }
    if (f == 3 || f == 4) {
      display.fillCircle(30, 37, 15 - (f - 3) * 5, SSD1306_WHITE);
    }
    drawPixelBall(58, 34, f);
    display.display();
    delay(80);
  }

  // 4) Ball lands, then shakes three times.
  for (int f = 0; f < 14; f++) {
    display.clearDisplay();
    int shake = 0;
    if (f >= 3) {
      int p = (f - 3) % 4;
      shake = (p == 1) ? -3 : ((p == 3) ? 3 : 0);
    }
    int dropY = (f < 3) ? 26 + f * 5 : 41;
    drawPixelBall(58 + shake, dropY, f);
    if (f >= 3) centerText("...", 8);
    display.display();
    delay(f < 3 ? 70 : 105);
  }

  // 5) Gotcha + title reveal.
  display.clearDisplay();
  drawPixelBall(58, 38, 0);
  centerText("Gotcha!", 10);
  centerText("Pikachu was caught!", 54);
  display.display();
  delay(700);

}

void drawWiFiConnecting(int dots) {
  display.clearDisplay();
  display.setTextColor(SSD1306_WHITE);
  display.setTextSize(1);
  centerText("POKEMON PARTNER", 4);
  centerText("Connecting Wi-Fi", 23);
  String d = "";
  for (int i = 0; i < dots; i++) d += ".";
  centerText(d, 36);
  display.drawRoundRect(14, 51, 100, 7, 3, SSD1306_WHITE);
  int w = dots * 30;
  if (w > 96) w = 96;
  if (w > 0) display.fillRoundRect(16, 53, w, 3, 1, SSD1306_WHITE);
  display.display();
}

void drawReadyScreen() {
  display.clearDisplay();
  display.setTextColor(SSD1306_WHITE);
  display.setTextSize(1);
  centerText("POKEMON PARTNER", 2);
  display.setCursor(18, 22); display.print("Wi-Fi     OK");
  display.setCursor(18, 34); display.print("Bridge    WAIT");
  centerText("Ready!", 51);
  display.display();
}

void drawGbcTrainer(int x, int y, int dir, int walkFrame) {
  const uint8_t* bmp = SPR_GBC_TRAINER_DOWN_0;
  if (dir == 1) bmp = SPR_GBC_TRAINER_LEFT;
  else if (dir == 2) bmp = SPR_GBC_TRAINER_RIGHT;
  else if (dir == 3) bmp = (walkFrame & 1) ? SPR_GBC_TRAINER_UP_1 : SPR_GBC_TRAINER_UP_0;
  else bmp = (walkFrame & 1) ? SPR_GBC_TRAINER_DOWN_1 : SPR_GBC_TRAINER_DOWN_0;
  display.drawBitmap(x, y, bmp, 16, 16, SSD1306_WHITE);
}

void drawGbcGrassTile(int x, int y, int frame, bool rustle = false) {
  const uint8_t* bmp = rustle ? SPR_GBC_GRASS_RUSTLE : ((frame & 1) ? SPR_GBC_GRASS_B : SPR_GBC_GRASS_A);
  display.drawBitmap(x, y, bmp, 8, 8, SSD1306_WHITE);
}

// --- Pokemon-Yellow-like scrolling overworld waiting scene -----------------
// The scene uses original monochrome sprites and an original route layout,
// but follows the classic handheld behavior: the trainer walks tile-by-tile,
// Pikachu trails one step behind, and the camera scrolls with the player.
const int ROUTE_WORLD_W = 256;
const int ROUTE_WORLD_H = 160;
const int ROUTE_LOOP_FRAMES = 240;

bool routeGrassTile(int tx, int ty) {
  // Three irregular tall-grass patches around the route.
  if (tx >= 4  && tx <= 12 && ty >= 3  && ty <= 7)  return true;
  if (tx >= 8  && tx <= 25 && ty >= 9  && ty <= 16) return true;
  if (tx >= 20 && tx <= 29 && ty >= 4  && ty <= 10) return true;
  return false;
}

bool routeShrubTile(int tx, int ty) {
  if (tx <= 0 || ty <= 0 || tx >= 31 || ty >= 19) return true;
  // Small tree/shrub clusters to make the route feel less empty.
  if ((tx >= 2 && tx <= 4 && ty >= 11 && ty <= 15) ||
      (tx >= 15 && tx <= 18 && ty >= 2 && ty <= 4) ||
      (tx >= 27 && tx <= 29 && ty >= 13 && ty <= 16)) return true;
  return false;
}

bool routeFenceTile(int tx, int ty) {
  // Short fence sections with gaps so the walking loop can pass through.
  if (ty == 8 && tx >= 2 && tx <= 7) return true;
  if (ty == 8 && tx >= 14 && tx <= 18) return true;
  if (tx == 19 && ty >= 11 && ty <= 15) return true;
  return false;
}

void drawRouteFenceTile(int x, int y, int tx, int ty) {
  display.drawFastHLine(x, y + 4, 8, SSD1306_WHITE);
  display.drawFastVLine(x + 1, y + 1, 7, SSD1306_WHITE);
  display.drawFastVLine(x + 6, y + 1, 7, SSD1306_WHITE);
  display.drawPixel(x + 1, y, SSD1306_WHITE);
  display.drawPixel(x + 6, y, SSD1306_WHITE);
}

void drawRouteGroundTile(int x, int y, int tx, int ty, int grassFrame) {
  if (routeShrubTile(tx, ty)) {
    display.drawBitmap(x, y, SPR_ROUTE_SHRUB, 8, 8, SSD1306_WHITE);
    return;
  }

  if (routeGrassTile(tx, ty)) {
    drawGbcGrassTile(x, y, grassFrame + ((tx + ty) & 1), false);
    return;
  }

  if (routeFenceTile(tx, ty)) {
    drawRouteFenceTile(x, y, tx, ty);
    return;
  }

  // Open route/path tile: very sparse texture so characters remain readable.
  if (((tx * 7 + ty * 11) & 7) == 0) display.drawPixel(x + 2, y + 2, SSD1306_WHITE);
  if (((tx * 5 + ty * 3) & 15) == 3) display.drawPixel(x + 6, y + 5, SSD1306_WHITE);
}

void drawScrollingRoute(int cameraX, int cameraY, int grassFrame) {
  display.clearDisplay();

  int startTx = cameraX / 8;
  int startTy = cameraY / 8;
  int offsetX = -(cameraX % 8);
  int offsetY = -(cameraY % 8);

  // One extra row/column on each side prevents gaps while the camera scrolls.
  for (int row = -1; row <= 9; row++) {
    int ty = startTy + row;
    int sy = offsetY + row * 8;
    for (int col = -1; col <= 17; col++) {
      int tx = startTx + col;
      int sx = offsetX + col * 8;
      if (sx <= -8 || sx >= 128 || sy <= -8 || sy >= 64) continue;
      drawRouteGroundTile(sx, sy, tx, ty, grassFrame);
    }
  }
}

// Closed walking loop. Coordinates are world-pixel positions for the trainer's
// 16x16 cell. Speed is 2 pixels per animation frame (4 frames per 8px tile).
void computeWalkXYDir(int frame, int &x, int &y, int &dir) {
  int f = frame % ROUTE_LOOP_FRAMES;
  if (f < 0) f += ROUTE_LOOP_FRAMES;

  x = 40; y = 32; dir = 2;

  if (f < 32) {                 // right 64 px
    x = 40 + f * 2; y = 32; dir = 2;
  } else if (f < 56) {          // down 48 px
    int t = f - 32;
    x = 104; y = 32 + t * 2; dir = 0;
  } else if (f < 96) {          // right 80 px
    int t = f - 56;
    x = 104 + t * 2; y = 80; dir = 2;
  } else if (f < 120) {         // down 48 px
    int t = f - 96;
    x = 184; y = 80 + t * 2; dir = 0;
  } else if (f < 160) {         // left 80 px
    int t = f - 120;
    x = 184 - t * 2; y = 128; dir = 1;
  } else if (f < 184) {         // up 48 px
    int t = f - 160;
    x = 104; y = 128 - t * 2; dir = 3;
  } else if (f < 216) {         // left 64 px
    int t = f - 184;
    x = 104 - t * 2; y = 80; dir = 1;
  } else {                      // up 48 px back to start
    int t = f - 216;
    x = 40; y = 80 - t * 2; dir = 3;
  }
}

void drawFollower(int x, int y, int frame) {
  const uint8_t* bmp = (frame & 1) ? SPR_FOLLOWER_1 : SPR_FOLLOWER_0;
  display.drawBitmap(x, y, bmp, 8, 8, SSD1306_WHITE);
}

bool routePixelInGrass(int worldX, int worldY) {
  int tx = (worldX + 8) / 8;
  int ty = (worldY + 12) / 8;
  return routeGrassTile(tx, ty);
}

void drawGrassForegroundAtWorld(int worldX, int worldY, int cameraX, int cameraY, int frame) {
  if (!routePixelInGrass(worldX, worldY)) return;
  int sx = worldX - cameraX;
  int sy = worldY - cameraY;
  // Cover the lower half of the trainer cell so it looks submerged in grass.
  drawGbcGrassTile(sx,     sy + 8, frame, true);
  drawGbcGrassTile(sx + 8, sy + 8, frame + 1, true);
}

// Party-head data is independent of wild encounter /state payloads.
// A matching PC bridge posts {"species":25,"nickname":"Pikachu","frame0":"...",
// "frame1":"..."} to /partner, with each 48x48 1bpp bitmap encoded as 576 hex chars.
// Until live lead-party data exists, never display a wild species as the partner.
String partnerNickname = "";
int partnerSpecies = 0;
uint8_t partnerFrame[2][288];
bool partnerFrameValid[2] = {false,false};

bool decodeHeadFrame(const String &hex, uint8_t *out) {
  if (hex.length()!=576) return false;
  for (int i=0;i<288;i++) {
    char hi=hex[i*2], lo=hex[i*2+1];
    auto nibble=[](char c)->int {
      if(c>='0'&&c<='9')return c-'0';
      if(c>='A'&&c<='F')return c-'A'+10;
      if(c>='a'&&c<='f')return c-'a'+10;
      return -1;
    };
    int h=nibble(hi),l=nibble(lo);
    if(h<0||l<0)return false;
    out[i]=(uint8_t)((h<<4)|l);
  }
  return true;
}

void handlePartner() {
  if (!server.hasArg("plain")) {server.send(400,"text/plain","Missing body");return;}
  JsonDocument doc;
  if (deserializeJson(doc,server.arg("plain"))) {server.send(400,"text/plain","Invalid JSON");return;}
  int newSpecies=doc["species"] | 0;
  if (newSpecies<1||newSpecies>1025) {server.send(400,"text/plain","Invalid party species");return;}
  String newName=doc["nickname"] | "";
  if (newSpecies!=partnerSpecies) {partnerFrameValid[0]=false;partnerFrameValid[1]=false;}
  partnerSpecies=newSpecies;
  partnerNickname=clipped(newName,18);
  for(int i=0;i<2;i++) {
    String key=i==0?"frame0":"frame1";
    if(doc[key].is<const char*>()) {
      String hex=doc[key].as<String>();
      partnerFrameValid[i]=decodeHeadFrame(hex,partnerFrame[i]);
    }
  }
  server.send(200,"application/json","{\"ok\":true}");
}

void drawWaitingAnimated() {
  display.clearDisplay();
  display.setTextColor(SSD1306_WHITE);display.setTextSize(1);display.setTextWrap(false);
  if(partnerSpecies<=0) {
    centerText("POKEMON PARTNER",3);
    display.drawRoundRect(39,17,50,36,7,SSD1306_WHITE);
    // Waiting face: explicitly a connection indicator, not a fake Pokemon species.
    int blink=(idleFrame%48)<3;
    display.fillRect(51,30,6,blink?1:5,SSD1306_WHITE);
    display.fillRect(71,30,6,blink?1:5,SSD1306_WHITE);
    display.drawFastHLine(58,43,13,SSD1306_WHITE);
    centerText("SYNC PARTY...",56);
  } else {
    String title=partnerNickname.length()?partnerNickname:String("DEX #")+partnerSpecies;
    centerText(clipped(title,18),1);
    int f=(idleFrame/8)&1;
    if(partnerFrameValid[f] || partnerFrameValid[0]) {
      int frame=partnerFrameValid[f]?f:0;
      display.drawBitmap(40,11,partnerFrame[frame],48,48,SSD1306_WHITE);
    } else if(partnerSpecies==25) {
      // Existing species-specific Pikachu sprite enlarged to fit the 48px face area.
      const uint8_t *bmp=(idleFrame/8&1)?SPR_PIKA_1:SPR_PIKA_0;
      for(int y=0;y<24;y++)for(int x=0;x<24;x++) {
        if(pgm_read_byte(bmp+y*3+x/8) & (0x80>>(x%8)))display.fillRect(40+x*2,11+y*2,2,2,SSD1306_WHITE);
      }
    } else {
      // No made-up universal face for species without a supplied head sprite.
      centerText("HEAD SPRITE",26);
      centerText("NOT SYNCED",38);
    }
    const char *moods[]={"CALM","HAPPY","SLEEPY","CURIOUS"};
    centerText(moods[(idleFrame/85)%4],57);
  }
  if(WiFi.status()==WL_CONNECTED)display.fillRect(123,1,2,2,SSD1306_WHITE);
  display.display();
}

void playEncounterTransition() {
  // Short monochrome transition inspired by classic handheld battle intros.
  for (int i = 0; i < 3; i++) {
    display.invertDisplay(true);
    delay(45);
    display.invertDisplay(false);
    delay(45);
  }

  // Horizontal bars close over the overworld.
  for (int step = 0; step <= 8; step++) {
    display.clearDisplay();
    int h = step * 4;
    display.fillRect(0, 0, 128, h, SSD1306_WHITE);
    display.fillRect(0, 64 - h, 128, h, SSD1306_WHITE);
    if (step & 1) {
      for (int y = h + 2; y < 64 - h; y += 8) {
        display.drawFastHLine(0, y, 128, SSD1306_WHITE);
      }
    }
    display.display();
    delay(28);
  }

  display.clearDisplay();
  display.display();
  delay(35);
}

void printStars() {
  for (int i = 0; i < 4; i++) display.print(i < stars ? '*' : '-');
}

void header(int page) {
  display.setTextSize(1);
  display.setTextColor(SSD1306_WHITE);
  display.setTextWrap(false);
  display.setCursor(0, 0);
  if (shiny) display.print("S ");
  display.print(clipped(pokeName, shiny ? 10 : 12));
  display.setCursor(78, 0);
  printStars();
  display.setCursor(110, 0);
  display.print(page + 1);
  display.print("/4");
  display.drawFastHLine(0, 9, 128, SSD1306_WHITE);
}

void drawOverview() {
  display.clearDisplay();
  header(0);

  display.setCursor(0, 13);
  display.print("Lv."); display.print(level);
  display.print("  #"); display.print(species);
  display.setCursor(116, 13);
  display.print(shortGender());

  display.setCursor(0, 24);
  display.print(clipped(natureName, 10));
  if (natureEffect.length()) {
    display.print("  ");
    display.print(clipped(natureEffect, 10));
  }

  display.setCursor(0, 36);
  display.print("Ability: ");
  display.print(clipped(abilityName, 12));

  display.setCursor(0, 49);
  if (shiny) display.print("SHINY  ");
  display.print("WILD LIVE");
  display.display();
}

void drawStats() {
  display.clearDisplay();
  header(1);
  display.setCursor(0, 13);
  display.print("STATS");

  display.setCursor(0, 25);
  display.print("HP "); display.print(stats[0]);
  display.setCursor(64, 25);
  display.print("ATK "); display.print(stats[1]);

  display.setCursor(0, 37);
  display.print("DEF "); display.print(stats[2]);
  display.setCursor(64, 37);
  display.print("SPA "); display.print(stats[3]);

  display.setCursor(0, 49);
  display.print("SPD "); display.print(stats[4]);
  display.setCursor(64, 49);
  display.print("SPE "); display.print(stats[5]);
  display.display();
}

void printIVValue(int value) {
  if (value < 10) display.print('0');
  display.print(value);
  if (value == 31) display.print('*');
}

void drawIVs() {
  // Six horizontal proportional bars with numeric IV values and perfect marker.
  display.clearDisplay();
  display.setTextSize(1);
  display.setTextColor(SSD1306_WHITE);
  display.setCursor(0, 0);
  display.print(clipped(pokeName, 12));
  display.setCursor(95, 0);
  display.print("IVs");
  display.drawFastHLine(0, 9, 128, SSD1306_WHITE);
  const char* labels[] = {"HP", "ATK", "DEF", "SPA", "SPD", "SPE"};
  for (int i = 0; i < 6; ++i) {
    int y = 11 + i * 7;
    int value = constrain(ivs[i], 0, 31);
    display.setCursor(0, y);
    display.print(labels[i]);
    display.drawRect(28, y + 1, 70, 5, SSD1306_WHITE);
    if (value > 0) display.fillRect(29, y + 2, (value * 68) / 31, 3, SSD1306_WHITE);
    display.setCursor(102, y);
    if (value < 10) display.print('0');
    display.print(value);
    if (value == 31) display.print('*');
  }
  display.drawFastHLine(0, 54, 128, SSD1306_WHITE);
  display.setCursor(0, 56);
  display.print("PERF:"); display.print(perfectIVs);
  display.setCursor(88, 56);
  printStars();
  display.display();
}

void drawMoves() {
  display.clearDisplay();
  header(3);
  for (int i = 0; i < 4; i++) {
    display.setCursor(0, 13 + i * 12);
    display.print(i + 1);
    display.print(" ");
    display.print(clipped(moveName[i], 18));
  }
  display.display();
}

void drawCurrentPage() {
  if (lastUpdateMs == 0 || millis() - lastUpdateMs > ENCOUNTER_TIMEOUT_MS) {
    display.clearDisplay(); display.setTextColor(SSD1306_WHITE); display.setTextSize(1);
    centerText("WILD POKEMON",7); centerText("NO ACTIVE ENCOUNTER",30);
    centerText("WINDOWS: BACK",54); display.display();
    return;
  }

  if (currentPage == 0) drawOverview();
  else if (currentPage == 1) drawStats();
  else if (currentPage == 2) drawIVs();
  else drawMoves();
}

bool applyEncounterPayload(const String& payload) {
  JsonDocument doc;
  if (deserializeJson(doc, payload)) return false;
  int validSpecies = doc["species"] | 0;
  if (validSpecies < 1 || validSpecies > 1025) return false;
  bridgeSeen = true;
  lastBridgeMs = millis();

  String incomingName = doc["nickname"] | "Pokemon";
  int incomingSpecies = doc["species"] | 0;
  int incomingLevel = doc["level"] | 0;
  unsigned long incomingEncounterId = doc["encounterid"] | 0;
  bool wasStale = (lastUpdateMs == 0 || millis() - lastUpdateMs > ENCOUNTER_TIMEOUT_MS);
  bool newEncounter = wasStale
    || (incomingEncounterId != 0 && incomingEncounterId != encounterId)
    || incomingSpecies != species
    || incomingLevel != level
    || incomingName != pokeName;

  pokeName = incomingName;
  sourceName = doc["source"] | "LIVE";
  gameName = doc["game"] | "";
  natureName = doc["nature"] | "Unknown";
  natureEffect = doc["natureeffect"] | "";
  abilityName = doc["ability"] | "Unknown";
  genderName = doc["gender"] | "Unknown";
  species = incomingSpecies;
  level = incomingLevel;
  if (incomingEncounterId != 0) encounterId = incomingEncounterId;
  stars = constrain((int)(doc["stars"] | 1), 1, 4);
  perfectIVs = constrain((int)(doc["perfectivs"] | 0), 0, 6);
  shiny = doc["shiny"] | false;

  ivs[0] = doc["ivhp"]  | 0;
  ivs[1] = doc["ivatk"] | 0;
  ivs[2] = doc["ivdef"] | 0;
  ivs[3] = doc["ivspa"] | 0;
  ivs[4] = doc["ivspd"] | 0;
  ivs[5] = doc["ivspe"] | 0;

  evs[0] = doc["evhp"]  | 0;
  evs[1] = doc["evatk"] | 0;
  evs[2] = doc["evdef"] | 0;
  evs[3] = doc["evspa"] | 0;
  evs[4] = doc["evspd"] | 0;
  evs[5] = doc["evspe"] | 0;

  stats[0] = doc["stathp"]  | 0;
  stats[1] = doc["statatk"] | 0;
  stats[2] = doc["statdef"] | 0;
  stats[3] = doc["statspa"] | 0;
  stats[4] = doc["statspd"] | 0;
  stats[5] = doc["statspe"] | 0;

  moveName[0] = doc["move1"] | "-";
  moveName[1] = doc["move2"] | "-";
  moveName[2] = doc["move3"] | "-";
  moveName[3] = doc["move4"] | "-";

  lastUpdateMs = millis();

  if (newEncounter) {
    currentPage = 0;
    pageStartedMs = millis();
    // An encounter does not select the Wild Pokemon screen.
    encounterTransitionPending = false;
  }

  if (!pocketMenuVisible && pocketActiveApp == 2) drawCurrentPage();
  return true;
}

void handleState() {
  if (!server.hasArg("plain") || !applyEncounterPayload(server.arg("plain"))) {
    server.send(400, "text/plain", "Invalid encounter JSON"); return;
  }
  server.send(200, "application/json", "{\"ok\":true}");
}

void handleHeartbeat() {
  bridgeSeen = true;
  lastBridgeMs = millis();
  // Encounter heartbeat: keep current encounter visible without resetting page.
  lastUpdateMs = millis();
  server.send(200, "application/json", "{\"ok\":true}");
}

void handleHealth() {
  bridgeSeen = true;
  lastBridgeMs = millis();
  String body = "{\"ok\":true,\"ip\":\"" + WiFi.localIP().toString() + "\",\"encounter\":\"" + pokeName + "\"}";
  server.send(200, "application/json", body);
}

void drawPocketMenu() {
  display.clearDisplay();
  display.setTextSize(1); display.setTextColor(SSD1306_WHITE);
  display.fillRect(0, 0, 128, 11, SSD1306_WHITE);
  display.setTextColor(SSD1306_BLACK); display.setCursor(3, 2); display.print("POCKET OS");
  display.setTextColor(SSD1306_WHITE);
  int begin = (pocketMenuIndex / 4) * 4;
  for (int i = 0; i < 4 && begin + i < pocketAppCount; ++i) {
    int item = begin + i; int y = 14 + i * 10;
    if (item == pocketMenuIndex) {
      display.fillRect(1, y - 1, 126, 10, SSD1306_WHITE);
      display.setTextColor(SSD1306_BLACK);
    }
    display.setCursor(4, y); display.print(item == pocketMenuIndex ? ">" : " ");
    display.print(pocketApps[item]);
    display.setTextColor(SSD1306_WHITE);
  }
  display.drawFastHLine(0, 55, 128, SSD1306_WHITE);
  display.setCursor(0, 56); display.print("WIN CTRL");
  display.setCursor(105, 56); display.print(pocketMenuIndex + 1); display.print("/9");
  display.display();
}

void drawPocketApp() {
  if (pocketActiveApp == 0) { drawWaitingAnimated(); return; }
  if (pocketActiveApp == 1) { drawIVs(); return; }
  if (pocketActiveApp == 2) { drawCurrentPage(); return; }
  if (pocketActiveApp == 3) {
    display.clearDisplay(); display.setTextSize(1); display.setTextColor(SSD1306_WHITE);
    display.setCursor(3, 3); display.print("SHINY HUNT");
    display.drawFastHLine(0, 13, 128, SSD1306_WHITE);
    display.setCursor(3, 23); display.print(pocketPrefs.getBool("shinyhunt",false)?"ENABLED":"DISABLED");
    display.setCursor(3, 39); display.print("Windows controlled"); display.display(); return;
  }
  display.clearDisplay(); display.setTextColor(SSD1306_WHITE); display.setTextSize(1);
  display.fillRect(0,0,128,12,SSD1306_WHITE); display.setTextColor(SSD1306_BLACK);
  display.setCursor(3,2); display.print(pocketApps[pocketActiveApp]);
  display.setTextColor(SSD1306_WHITE);
  if (pocketActiveApp == 4) {
    struct tm tmv;
    if (getLocalTime(&tmv, 5)) { char buf[24]; strftime(buf,sizeof(buf),"%H:%M:%S",&tmv); display.setTextSize(2); display.setCursor(12,26); display.print(buf); }
    else { display.setCursor(5,28); display.print("Waiting for NTP"); }
  } else if (pocketActiveApp == 5) { display.setCursor(5,29); display.print("PocketOS device info"); }
  else if (pocketActiveApp == 6) {
    display.setCursor(3,21); display.print(WiFi.status()==WL_CONNECTED?"Wi-Fi: CONNECTED":"Wi-Fi: OFFLINE");
    display.setCursor(3,34); display.print(WiFi.localIP());
    display.setCursor(3,47); display.print(WiFi.RSSI()); display.print(" dBm");
  } else if (pocketActiveApp == 7) { display.setCursor(5,28); display.print("Awaiting PC data"); }
  else { display.setCursor(5,24); display.print("Managed from PC"); display.setCursor(5,38); display.print("OLED / Wi-Fi"); }
  display.display();
}
void applyPocketCommand(String action) {
  action.trim(); action.toLowerCase();
  if (action == "menu") { pocketMenuVisible = true; drawPocketMenu(); }
  else if (action == "up" || action == "down") {
    pocketMenuVisible = true;
    pocketMenuIndex = (pocketMenuIndex + (action == "up" ? pocketAppCount - 1 : 1)) % pocketAppCount;
    drawPocketMenu();
  } else if (action == "select") {
    pocketMenuVisible = false; pocketActiveApp=pocketMenuIndex;
    drawPocketApp();
  } else if (action == "back") {
    pocketMenuVisible = true; drawPocketMenu();
  } else if (action == "pokemon") {
    pocketMenuVisible = false; pocketActiveApp=0; drawWaitingAnimated();
  } else if (action == "ivs") { pocketMenuVisible = false; pocketActiveApp=1; drawIVs(); }
  else if (action == "moves") { pocketMenuVisible = false; pocketActiveApp=2; currentPage = 3; drawMoves(); }
  else if (action.startsWith("app:")) {
    int id=action.substring(4).toInt();
    if (id>=0 && id<pocketAppCount) {pocketMenuVisible=false;pocketMenuIndex=id;pocketActiveApp=id;drawPocketApp();}
  }
}

void handlePocketControl() {
  if (!server.hasArg("plain")) { server.send(400, "text/plain", "Missing command"); return; }
  JsonDocument doc;
  if (deserializeJson(doc, server.arg("plain"))) { server.send(400, "text/plain", "Invalid JSON"); return; }
  String action = doc["action"] | "";
  applyPocketCommand(action);
  server.send(200, "application/json", "{\"ok\":true}");
}

void processPocketSerial(String line) {
  line.trim();
  if (line == "WIFI_START") {
    startPocketWifiOnce();
    Serial.println("WIFI_START_ACK");
  } else if (line == "WIFI_SCAN") {
    if (!pocketWifiStarted) startPocketWifiOnce();
    int count = WiFi.scanNetworks(false, true);
    if (count < 0) { Serial.println("WIFI_ERROR scan_failed"); return; }
    for (int i = 0; i < count; ++i) {
      String name = WiFi.SSID(i);
      if (name.length() == 0) continue;
      name.replace("|", "_"); name.replace("\n", " ");
      String security = WiFi.encryptionType(i) == WIFI_AUTH_OPEN ? "Open" : "Secured";
      Serial.println("WIFI_NETWORK " + name + "|" + String(WiFi.RSSI(i)) + "|" + security);
    }
    WiFi.scanDelete();
    Serial.println("WIFI_SCAN_DONE");
  } else if (line.startsWith("WIFI_JOIN ")) {
    JsonDocument doc;
    if (deserializeJson(doc, line.substring(10))) { Serial.println("WIFI_ERROR invalid_json"); return; }
    String ssid = doc["ssid"] | ""; String pass = doc["password"] | "";
    if (ssid.isEmpty() || ssid.length() > 32 || pass.length() > 63) { Serial.println("WIFI_ERROR invalid_credentials"); return; }
    pocketPrefs.putString("ssid", ssid);
    pocketPrefs.putString("pass", pass);
    if (!pocketWifiStarted) startPocketWifiOnce();
    WiFi.begin(ssid.c_str(), pass.c_str());
    lastWifiRetryMs = millis();
    Serial.println("WIFI_CONNECTING " + ssid);
  } else if (line.startsWith("POCKET_ENCOUNTER_JSON ")) {
    // USB-first game data: same field decoder as the legacy Wi-Fi /state route.
    bool ok = applyEncounterPayload(line.substring(22));
    Serial.println(ok ? "POCKET_ENCOUNTER_OK" : "POCKET_ENCOUNTER_ERROR");
  } else if (line == "POCKET_ENCOUNTER_HEARTBEAT") {
    if (lastUpdateMs != 0) lastUpdateMs = millis();
    lastBridgeMs = millis();
    Serial.println("POCKET_ENCOUNTER_OK");
  } else if (line == "POCKET_DIAG") {
    Serial.printf("POCKET_DIAG uptime=%lu heap=%u wifi=%d menu=%d\n", millis(), (unsigned)ESP.getFreeHeap(), (int)WiFi.status(), (int)pocketMenuVisible);
  } else if (line == "WIFI_STATUS") {
    if (WiFi.status() == WL_CONNECTED) Serial.println("WIFI_CONNECTED " + WiFi.localIP().toString());
    else Serial.println("WIFI_DISCONNECTED");
  } else if (line == "POCKET_GET_FRAME") {
    // SSD1306 native page-order framebuffer: 1024 bytes encoded as 2048 hex characters.
    // Return only after initialization so the Windows mirror is pixel-identical.
    const uint8_t* pixels = display.getBuffer();
    const char hex[] = "0123456789ABCDEF";
    Serial.print("POCKET_FRAME ");
    for (int k = 0; k < SCREEN_WIDTH * SCREEN_HEIGHT / 8; ++k) {
      uint8_t v = pixels[k];
      Serial.write(hex[v >> 4]);
      Serial.write(hex[v & 15]);
    }
    Serial.println();
  } else if (line == "POCKET_GET_STATE") {
    Serial.println("POCKET_STATE " + String(pocketMenuVisible ? "MENU" : "APP") + "|" + String(pocketMenuIndex) + "|" + String(pocketActiveApp));
  } else if (line == "POCKET_VERSION") { Serial.println("POCKET_VERSION 1.0.0-beta");
  } else if (line.startsWith("POCKET_HUNT ")) {
    pocketPrefs.putBool("shinyhunt",line.substring(12)=="1");
    Serial.println("POCKET_OK");
  } else if (line.startsWith("POCKET_CMD ")) {
    applyPocketCommand(line.substring(11)); Serial.println("POCKET_STATE " + String(pocketMenuVisible ? "MENU" : "APP") + "|" + String(pocketMenuIndex) + "|" + String(pocketActiveApp)); Serial.println("POCKET_OK");
  }
}
void pollPocketSerial() {
  while (Serial.available() > 0) {
    char c = (char)Serial.read();
    if (c == '\n') { processPocketSerial(serialLine); serialLine = ""; }
    else if (c != '\r' && serialLine.length() < 4096) serialLine += c;
  }
}

// Delay radio startup so the startup graphics and Wi-Fi load do not overlap.
// Keep USB serial provisioning available before Wi-Fi comes online.
void startPocketWifiOnce() {
  if (pocketWifiStarted) return;
  pocketWifiStarted = true;
  Serial.println("BOOT_STAGE WIFI_INIT");
  WiFi.mode(WIFI_STA);
  WiFi.setSleep(true); // do not force high-power radio operation
  String savedSsid = pocketPrefs.getString("ssid", "");
  if (savedSsid.length()) {
    String savedPass = pocketPrefs.getString("pass", "");
    WiFi.begin(savedSsid.c_str(), savedPass.c_str());
  } else if (String(WIFI_SSID) != "YOUR_WIFI_NAME") {
    WiFi.begin(WIFI_SSID, WIFI_PASS);
  }
  server.begin();
  Serial.println("BOOT_STAGE WIFI_STARTED");
}

void setup() {
  Serial.begin(115200);
  delay(120);
  Serial.printf("BOOT_STAGE RESET_REASON=%d FREE_HEAP=%u\n", (int)esp_reset_reason(), (unsigned)ESP.getFreeHeap());
  Wire.begin(21, 22);
  Wire.setClock(100000);
  Serial.println("BOOT_STAGE OLED_INIT");

  if (!display.begin(SSD1306_SWITCHCAPVCC, OLED_ADDR)) {
    Serial.println("SSD1306 init failed");
    while (true) delay(1000);
  }

  Serial.println("BOOT_STAGE INTRO_START");
  playBootAnimation();
  lazyTechReveal();
  Serial.println("BOOT_STAGE INTRO_DONE");

  pocketPrefs.begin("pocketwifi", false);
  // Configure time only after the radio is connected, never before showing menu.
  Serial.println("BOOT_STAGE WIFI_DEFERRED_USB_ONLY");
  Serial.println("BOOT_STAGE MENU_INIT");
  server.on("/pocketos/control", HTTP_POST, handlePocketControl);
  server.on("/health", HTTP_GET, handleHealth);
  server.on("/heartbeat", HTTP_GET, handleHeartbeat);
  server.on("/state", HTTP_POST, handleState);
  server.on("/partner", HTTP_POST, handlePartner);
  // Web server is started only when Wi-Fi is enabled.

  // Boot to launcher. Do not auto-launch Pokemon Partner.
  pocketMenuVisible = true;
  pocketMenuIndex = 0;
  pocketActiveApp = 0;
  drawPocketMenu();
  Serial.printf("POCKETOS_READY HEAP=%u\n", (unsigned)ESP.getFreeHeap());
}

void loop() {
  static unsigned long lastPocketDiag = 0;
  if (millis() - lastPocketDiag >= 15000) {
    lastPocketDiag = millis();
    Serial.printf("POCKET_ALIVE uptime=%lu heap=%u\n", millis(), (unsigned)ESP.getFreeHeap());
  }
  pollPocketSerial();
  if (pocketWifiStarted) server.handleClient();
  if (pocketWifiStarted && WiFi.status() != WL_CONNECTED && millis() - lastWifiRetryMs > WIFI_RETRY_MS) {
    lastWifiRetryMs = millis();
    if (pocketPrefs.getString("ssid", "").length()) WiFi.reconnect();
  }
  if (pocketMenuVisible) { delay(1); return; }
  // Wild encounters are tracked in the background, without changing the OLED screen.
  // Only an explicit Windows app selection (app:2) shows encounter pages.
  if (pocketActiveApp == 2) {
    if (lastUpdateMs && millis()-lastUpdateMs<=ENCOUNTER_TIMEOUT_MS
        && millis()-pageStartedMs>=PAGE_DURATION_MS) {
      currentPage=(currentPage+1)%PAGE_COUNT;
      pageStartedMs=millis();
      drawCurrentPage();
    }
  } else if (pocketActiveApp == 0) {
    if (millis()-lastIdleFrameMs>=IDLE_FRAME_MS) {
      lastIdleFrameMs=millis();idleFrame++;drawWaitingAnimated();
    }
  } else if (pocketActiveApp == 1) {
    static unsigned long lastIvRedraw=0;
    if(millis()-lastIvRedraw>=1000){lastIvRedraw=millis();drawIVs();}
  } else {
    static unsigned long lastAppRedraw=0;
    if(millis()-lastAppRedraw>=1000){lastAppRedraw=millis();drawPocketApp();}
  }

  delay(1);
}
