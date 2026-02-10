#pragma once

// RGB LED status indication using NeoPixel on GPIO 48
#define RGB_PIN       48
#define RGB_COUNT     1
#ifdef RGB_BRIGHTNESS
#undef RGB_BRIGHTNESS
#endif
#define RGB_BRIGHTNESS 30  // Safe brightness for onboard LED

// LED States
enum LedState {
    LED_OFF,              // All LEDs off
    LED_BOOT,             // Solid red - device booting
    LED_CONNECTING,       // Solid blue - waiting for WiFi connection
    LED_CONNECTED,        // Solid green - successfully connected
    LED_ERROR             // Error with blink code (deprecated - use setLedError)
};

// Error codes for LED blink patterns
enum LedErrorCode {
    ERROR_NONE = 0,
    
    // WiFi errors (BLUE blinks)
    ERROR_WIFI_TIMEOUT = 1,       // 1 blue blink - WiFi connection timeout
    ERROR_WIFI_NO_SSID = 2,       // 2 blue blinks - SSID not found
    ERROR_WIFI_AUTH_FAIL = 3,     // 3 blue blinks - Authentication failed (wrong password)
    ERROR_WIFI_DISCONNECTED = 4,  // 4 blue blinks - Connection lost/disconnected
    ERROR_WIFI_GENERIC = 5,       // 5 blue blinks - Other WiFi error
    
    // Network/Gateway errors (RED blinks)
    ERROR_MDNS = 11,              // 1 red blink - mDNS/Gateway discovery failed
    ERROR_HEARTBEAT = 12,         // 2 red blinks - Heartbeat communication failed
    ERROR_SENSOR_SEND = 13,       // 3 red blinks - Sensor reading send failed
    ERROR_SENSOR_READ = 14,       // 4 red blinks - Sensor hardware read error
    ERROR_LOW_VOLTAGE = 15        // 5 red blinks - Supply voltage too low
};

// Initialize RGB LED (NeoPixel)
void initRgbLed();

// Set the current LED state
void setLedState(LedState state);

// Set LED to error state with specific error code
void setLedError(LedErrorCode errorCode);

// Clear error state (return to normal operation)
void clearLedError();

// Update LED animation (call this in loop)
void updateLed();
