#include "fsconfig.h"
#include "logging.h"
#include <LittleFS.h>
#include <ArduinoJson.h>

static DeviceConfig g_deviceConfig;
static bool g_configLoaded = false;

bool initFsConfig()
{
    logLine("Initializing LittleFS...");
    
    // Initialize LittleFS
    if (!LittleFS.begin())
    {
        logLine("ERROR: LittleFS mount failed!");
        logLine("Make sure to upload filesystem with Pico-W-FS tool");
        return false;
    }
    
    logLine("LittleFS initialized successfully");
    
    // Open config.json
    File configFile = LittleFS.open("/config.json", "r");
    if (!configFile)
    {
        logLine("ERROR: Failed to open /config.json");
        logLine("Upload config.json to filesystem: Place in data/ folder and upload FS");
        return false;
    }
    
    logLine("Reading config.json...");
    
    // Allocate JsonDocument (using 2KB for config)
    JsonDocument doc;
    
    // Parse JSON
    DeserializationError error = deserializeJson(doc, configFile);
    configFile.close();
    
    if (error)
    {
        logLine("ERROR: Failed to parse config.json: " + String(error.c_str()));
        return false;
    }
    
    // Extract WiFi section
    JsonObject wifi = doc["WiFi"];
    if (!wifi)
    {
        logLine("ERROR: Missing 'WiFi' section in config.json");
        return false;
    }
    
    strncpy(g_deviceConfig.wifiSSID, wifi["SSID"] | "", MAX_WIFI_SSID_LEN - 1);
    strncpy(g_deviceConfig.wifiPassword, wifi["Password"] | "", MAX_WIFI_PASSWORD_LEN - 1);
    
    if (strlen(g_deviceConfig.wifiSSID) == 0)
    {
        logLine("ERROR: WiFi SSID is empty in config.json");
        return false;
    }
    
    // Extract Device section
    JsonObject device = doc["Device"];
    if (!device)
    {
        logLine("ERROR: Missing 'Device' section in config.json");
        return false;
    }
    
    strncpy(g_deviceConfig.clientId, device["ClientId"] | "pico2w-001", MAX_DEVICE_ID_LEN - 1);
    strncpy(g_deviceConfig.deviceType, device["DeviceType"] | "Pico2W", MAX_DEVICE_TYPE_LEN - 1);
    strncpy(g_deviceConfig.friendlyName, device["FriendlyName"] | "Pico 2W Client", MAX_FRIENDLY_NAME_LEN - 1);
    g_deviceConfig.commandListenerPort = device["CommandListenerPort"] | 8888;
    
    // Extract Intervals section
    JsonObject intervals = doc["Intervals"];
    g_deviceConfig.heartbeatSeconds = intervals["HeartbeatSeconds"] | 60;
    // SensorReadingSeconds removed - now per-sensor
    
    // Extract Hardware.Relays array
    JsonArray relays = doc["Hardware"]["Relays"];
    g_deviceConfig.relayCount = 0;
    
    if (relays)
    {
        for (JsonObject relay : relays)
        {
            if (g_deviceConfig.relayCount >= MAX_RELAYS)
            {
                logLine("WARNING: More than " + String(MAX_RELAYS) + " relays in config, ignoring extras");
                break;
            }
            
            RelayConfig& r = g_deviceConfig.relays[g_deviceConfig.relayCount];
            
            strncpy(r.id, relay["Id"] | "", MAX_RELAY_ID_LEN - 1);
            strncpy(r.name, relay["Name"] | "Relay", MAX_RELAY_NAME_LEN - 1);
            r.pin = relay["Pin"] | -1;
            r.initialState = relay["InitialState"] | false;
            r.enabled = relay["Enabled"] | true;
            
            if (r.pin < 0 || strlen(r.id) == 0)
            {
                logLine("WARNING: Invalid relay config at index " + String(g_deviceConfig.relayCount));
                continue;
            }
            
            g_deviceConfig.relayCount++;
        }
    }
    
    // Extract Hardware.Sensors array (optional)
    g_deviceConfig.sensorCount = 0;
    JsonArray sensors = doc["Hardware"]["Sensors"];
    
    if (sensors)
    {
        for (JsonObject sensor : sensors)
        {
            if (g_deviceConfig.sensorCount >= MAX_SENSORS)
            {
                logLine("WARNING: More than " + String(MAX_SENSORS) + " sensors in config, ignoring extras");
                break;
            }
            
            SensorConfig& s = g_deviceConfig.sensors[g_deviceConfig.sensorCount];
            
            strncpy(s.id, sensor["Id"] | "", MAX_SENSOR_ID_LEN - 1);
            strncpy(s.type, sensor["Type"] | "", MAX_SENSOR_TYPE_LEN - 1);
            strncpy(s.name, sensor["Name"] | "Sensor", MAX_SENSOR_NAME_LEN - 1);
            strncpy(s.i2cAddress, sensor["I2cAddress"] | "0x44", MAX_SENSOR_I2C_ADDR_LEN - 1);
            s.enabled = sensor["Enabled"] | false;
            s.readingIntervalSeconds = sensor["ReadingIntervalSeconds"] | 300;  // Default 5 minutes
            
            if (strlen(s.id) == 0 || strlen(s.type) == 0)
            {
                logLine("WARNING: Invalid sensor config at index " + String(g_deviceConfig.sensorCount));
                continue;
            }
            
            g_deviceConfig.sensorCount++;
        }
    }
    
    // Extract Hardware.Buttons array
    JsonArray buttons = doc["Hardware"]["Buttons"];
    g_deviceConfig.buttonCount = 0;
    
    if (buttons)
    {
        for (JsonObject button : buttons)
        {
            if (g_deviceConfig.buttonCount >= MAX_BUTTONS)
            {
                logLine("WARNING: More than " + String(MAX_BUTTONS) + " buttons in config, ignoring extras");
                break;
            }
            
            ButtonConfig& b = g_deviceConfig.buttons[g_deviceConfig.buttonCount];
            
            strncpy(b.id, button["Id"] | "", MAX_RELAY_ID_LEN - 1);
            strncpy(b.name, button["Name"] | "Button", MAX_RELAY_NAME_LEN - 1);
            b.pin = button["Pin"] | -1;
            strncpy(b.targetDevice, button["TargetDevice"] | "", MAX_DEVICE_ID_LEN - 1);
            strncpy(b.targetRelay, button["TargetRelay"] | "", MAX_RELAY_ID_LEN - 1);
            b.enabled = button["Enabled"] | false;
            
            if (b.pin >= 0 && strlen(b.targetDevice) > 0 && strlen(b.targetRelay) > 0 && b.enabled)
            {
                g_deviceConfig.buttonCount++;
            }
            else
            {
                logLine("WARNING: Skipping invalid button config: " + String(b.name));
            }
        }
    }
    
    // Log loaded configuration
    logLine("Configuration loaded successfully:");
    logLine("  WiFi SSID: " + String(g_deviceConfig.wifiSSID));
    logLine("  WiFi Password: " + String(g_deviceConfig.wifiPassword));
    logLine("  ClientId: " + String(g_deviceConfig.clientId));
    logLine("  Type: " + String(g_deviceConfig.deviceType));
    logLine("  Name: " + String(g_deviceConfig.friendlyName));
    logLine("  Command Port: " + String(g_deviceConfig.commandListenerPort));
    logLine("  Heartbeat: " + String(g_deviceConfig.heartbeatSeconds) + "s");
    logLine("  (Sensor intervals configured per-sensor below)");
    logLine("  Relays: " + String(g_deviceConfig.relayCount));
    
    for (int i = 0; i < g_deviceConfig.relayCount; i++)
    {
        RelayConfig& r = g_deviceConfig.relays[i];
        logLine("    [" + String(r.id) + "] " + String(r.name) + 
                " - Pin:" + String(r.pin) + 
                " Init:" + String(r.initialState ? "ON" : "OFF"));
    }
    
    logLine("  Sensors: " + String(g_deviceConfig.sensorCount));
    
    for (int i = 0; i < g_deviceConfig.sensorCount; i++)
    {
        SensorConfig& s = g_deviceConfig.sensors[i];
        logLine("    [" + String(s.id) + "] " + String(s.type) + 
                " - " + String(s.name) + 
                " I2C:" + String(s.i2cAddress) +
                " Enabled:" + String(s.enabled ? "YES" : "NO") +
                " Interval:" + String(s.readingIntervalSeconds) + "s");
    }
    
    logLine("  Buttons: " + String(g_deviceConfig.buttonCount));
    
    for (int i = 0; i < g_deviceConfig.buttonCount; i++)
    {
        ButtonConfig& b = g_deviceConfig.buttons[i];
        logLine("    [" + String(b.id) + "] " + String(b.name) + 
                " - Pin:" + String(b.pin) + 
                " Target:" + String(b.targetDevice) + ":" + String(b.targetRelay));
    }
    
    g_configLoaded = true;
    return true;
}

const DeviceConfig& getDeviceConfig()
{
    return g_deviceConfig;
}

bool isFsConfigLoaded()
{
    return g_configLoaded;
}
