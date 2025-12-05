#ifndef SDCONFIG_H
#define SDCONFIG_H

#include <Arduino.h>

// Maximum sizes for config strings
#define MAX_DEVICE_ID_LEN 32
#define MAX_DEVICE_TYPE_LEN 32
#define MAX_FRIENDLY_NAME_LEN 64
#define MAX_RELAY_ID_LEN 16
#define MAX_RELAY_NAME_LEN 32
#define MAX_RELAYS 4
#define MAX_SENSOR_ID_LEN 16
#define MAX_SENSOR_TYPE_LEN 16
#define MAX_SENSOR_NAME_LEN 32
#define MAX_SENSOR_I2C_ADDR_LEN 8
#define MAX_SENSORS 4
#define MAX_WIFI_SSID_LEN 32
#define MAX_WIFI_PASSWORD_LEN 64
#define MAX_BUTTON_ID_LEN 16
#define MAX_BUTTON_NAME_LEN 32
#define MAX_BUTTONS 8

// Button configuration structure
struct ButtonConfig
{
    char id[MAX_BUTTON_ID_LEN];
    char name[MAX_BUTTON_NAME_LEN];
    int pin;
    char targetDevice[MAX_DEVICE_ID_LEN];
    char targetRelay[MAX_RELAY_ID_LEN];
    bool enabled;
};

// Relay configuration structure
struct RelayConfig
{
    char id[MAX_RELAY_ID_LEN];
    char name[MAX_RELAY_NAME_LEN];
    int pin;
    bool initialState;
    bool enabled;
};

// Sensor configuration structure
struct SensorConfig
{
    char id[MAX_SENSOR_ID_LEN];
    char type[MAX_SENSOR_TYPE_LEN];
    char name[MAX_SENSOR_NAME_LEN];
    char i2cAddress[MAX_SENSOR_I2C_ADDR_LEN];
    bool enabled;
    int readingIntervalSeconds;  // Per-sensor interval (default 300 = 5 minutes)
};

// Device configuration structure
struct DeviceConfig
{
    char wifiSSID[MAX_WIFI_SSID_LEN];
    char wifiPassword[MAX_WIFI_PASSWORD_LEN];
    
    char clientId[MAX_DEVICE_ID_LEN];
    char deviceType[MAX_DEVICE_TYPE_LEN];
    char friendlyName[MAX_FRIENDLY_NAME_LEN];
    int commandListenerPort;
    
    int heartbeatSeconds;
    // sensorReadingSeconds removed - now per-sensor in SensorConfig.readingIntervalSeconds
    
    RelayConfig relays[MAX_RELAYS];
    int relayCount;
    
    SensorConfig sensors[MAX_SENSORS];
    int sensorCount;
    
    ButtonConfig buttons[MAX_BUTTONS];
    int buttonCount;
};

/**
 * Initialize SD card and load configuration from config.json
 * Returns true if successful, false otherwise
 */
bool initSdConfig();

/**
 * Get the loaded device configuration
 */
const DeviceConfig& getDeviceConfig();

/**
 * Check if SD config was successfully loaded
 */
bool isSdConfigLoaded();

#endif // SDCONFIG_H
