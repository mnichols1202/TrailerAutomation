#include "commandlistener.h"
#include "sdconfig.h"
#include "relaycontrol.h"
#include "logging.h"
#include <WiFi.h>
#include <ArduinoJson.h>
#include <FS.h>
#include <LittleFS.h>

static WiFiServer* g_commandServer = nullptr;
static bool g_listenerInitialized = false;

bool initCommandListener()
{
    if (!isSdConfigLoaded())
    {
        logLine("ERROR: Cannot start command listener - config not loaded");
        return false;
    }
    
    const DeviceConfig& config = getDeviceConfig();
    
    // Stop existing listener if running (for re-initialization)
    if (g_listenerInitialized && g_commandServer)
    {
        logLine("Stopping existing command listener for re-initialization...");
        g_commandServer->stop();
        delete g_commandServer;
        g_commandServer = nullptr;
        g_listenerInitialized = false;
        delay(100);  // Brief delay for port to be released
    }
    
    logLine("Starting TCP command listener on port " + String(config.commandListenerPort) + "...");
    
    // Create and start server
    g_commandServer = new WiFiServer(config.commandListenerPort);
    g_commandServer->begin();
    
    g_listenerInitialized = true;
    logLine("Command listener started successfully");
    
    return true;
}

void processCommandListener()
{
    if (!g_listenerInitialized || !g_commandServer)
    {
        return;
    }
    
    // Check for incoming client connections
    WiFiClient client = g_commandServer->available();
    
    if (!client)
    {
        return;  // No client connected
    }
    
    logLine("[CommandListener] Client connected from " + client.remoteIP().toString());
    
    // Wait for data with timeout (5 seconds)
    unsigned long startTime = millis();
    while (!client.available() && millis() - startTime < 5000)
    {
        delay(10);
    }
    
    if (!client.available())
    {
        logLine("[CommandListener] Timeout waiting for data");
        client.stop();
        return;
    }
    
    // Read command line (JSON terminated by \n)
    String commandLine = client.readStringUntil('\n');
    commandLine.trim();
    
    if (commandLine.length() == 0)
    {
        logLine("[CommandListener] Empty command received");
        client.stop();
        return;
    }
    
    logLine("[CommandListener] Received: " + commandLine);
    logLine("[CommandListener] Command length: " + String(commandLine.length()) + " bytes");
    
    // Strip UTF-8 BOM if present (0xEF 0xBB 0xBF)
    if (commandLine.length() >= 3 && 
        (unsigned char)commandLine[0] == 0xEF && 
        (unsigned char)commandLine[1] == 0xBB && 
        (unsigned char)commandLine[2] == 0xBF)
    {
        commandLine = commandLine.substring(3);
        logLine("[CommandListener] Stripped UTF-8 BOM from command");
    }
    
    // Parse JSON command - ArduinoJson v7 auto-allocates memory
    JsonDocument cmdDoc;
    DeserializationError error = deserializeJson(cmdDoc, commandLine);
    
    if (error)
    {
        logLine("[CommandListener] JSON parse error: " + String(error.c_str()));
        logLine("[CommandListener] Error code: " + String((int)error.code()));
        logLine("[CommandListener] Raw command: [" + commandLine + "]");
        
        // Send error response
        JsonDocument respDoc;
        respDoc["success"] = false;
        respDoc["message"] = String("Invalid JSON: ") + error.c_str();
        respDoc["errorCode"] = "JSON_ERROR";
        
        String response;
        serializeJson(respDoc, response);
        client.println(response);
        client.stop();
        return;
    }
    
    // Extract command fields
    const char* commandId = cmdDoc["commandId"] | "unknown";
    const char* commandType = cmdDoc["type"] | "";
    
    // Handle command based on type
    JsonDocument respDoc;
    respDoc["commandId"] = commandId;
    
    if (strcmp(commandType, "ping") == 0)
    {
        // Simple ping/pong response for connectivity check
        respDoc["success"] = true;
        respDoc["message"] = "pong";
        
        logLine("[CommandListener] Ping received, responding with pong");
    }
    else if (strcmp(commandType, "identify") == 0)
    {
        // Return actual ClientId so gateway can verify identity
        const DeviceConfig& config = getDeviceConfig();
        
        respDoc["success"] = true;
        respDoc["message"] = "Device identified";
        
        JsonObject data = respDoc["data"].to<JsonObject>();
        data["clientId"] = config.clientId;
        data["deviceType"] = config.deviceType;
        data["friendlyName"] = config.friendlyName;
        
        logLine("[CommandListener] Identify command received, responding with ClientId: " + String(config.clientId));
    }
    else if (strcmp(commandType, "init") == 0)
    {
        // Trigger re-initialization - mark as needing registration and send response
        extern bool g_deviceRegistered;
        g_deviceRegistered = false;  // Force re-registration on next loop
        
        respDoc["success"] = true;
        respDoc["message"] = "Device will re-initialize";
        
        logLine("[CommandListener] Init command received, will re-register with gateway");
    }
    else if (strcmp(commandType, "reboot") == 0)
    {
        // Send success response first, then reboot
        respDoc["success"] = true;
        respDoc["message"] = "Device rebooting";
        
        logLine("[CommandListener] Reboot command received, device will restart in 1 second");
        
        // Send response
        String response;
        serializeJson(respDoc, response);
        client.println(response);
        client.flush();
        client.stop();
        
        // Wait a moment for response to be sent, then reboot
        delay(1000);
        ESP.restart();
        return;  // Never reached
    }
    else if (strcmp(commandType, "setRelay") == 0)
    {
        // Extract payload
        JsonObject payload = cmdDoc["payload"];
        
        if (!payload)
        {
            respDoc["success"] = false;
            respDoc["message"] = "Missing payload";
            respDoc["errorCode"] = "MISSING_PAYLOAD";
        }
        else
        {
            const char* relayId = payload["relayId"];
            const char* stateStr = payload["state"];
            
            if (!relayId || !stateStr)
            {
                respDoc["success"] = false;
                respDoc["message"] = "Invalid payload: relayId and state required";
                respDoc["errorCode"] = "INVALID_PAYLOAD";
            }
            else
            {
                bool state = (strcmp(stateStr, "on") == 0);
                
                if (setRelayState(relayId, state))
                {
                    respDoc["success"] = true;
                    respDoc["message"] = String("Relay '") + relayId + "' set to " + stateStr;
                    
                    JsonObject data = respDoc["data"].to<JsonObject>();
                    data["relayId"] = relayId;
                    data["state"] = stateStr;
                }
                else
                {
                    respDoc["success"] = false;
                    respDoc["message"] = String("Failed to set relay '") + relayId + "'";
                    respDoc["errorCode"] = "RELAY_ERROR";
                }
            }
        }
    }
    else if (strcmp(commandType, "getRelayState") == 0)
    {
        // Extract payload
        JsonObject payload = cmdDoc["payload"];
        
        if (!payload)
        {
            respDoc["success"] = false;
            respDoc["message"] = "Missing payload";
            respDoc["errorCode"] = "MISSING_PAYLOAD";
        }
        else
        {
            const char* relayId = payload["relayId"];
            
            if (!relayId)
            {
                respDoc["success"] = false;
                respDoc["message"] = "Invalid payload: relayId required";
                respDoc["errorCode"] = "INVALID_PAYLOAD";
            }
            else
            {
                bool state = false;
                
                if (getRelayState(relayId, &state))
                {
                    const char* stateStr = state ? "on" : "off";
                    
                    respDoc["success"] = true;
                    respDoc["message"] = String("Relay '") + relayId + "' state retrieved";
                    
                    JsonObject data = respDoc["data"].to<JsonObject>();
                    data["relayId"] = relayId;
                    data["state"] = stateStr;
                }
                else
                {
                    respDoc["success"] = false;
                    respDoc["message"] = String("Relay '") + relayId + "' not found";
                    respDoc["errorCode"] = "RELAY_NOT_FOUND";
                }
            }
        }
    }
    else if (strcmp(commandType, "getConfig") == 0)
    {
        // Return device configuration
        const DeviceConfig& config = getDeviceConfig();
        
        respDoc["success"] = true;
        respDoc["message"] = "Configuration retrieved";
        
        JsonObject data = respDoc["data"].to<JsonObject>();
        
        // Device section
        JsonObject device = data["device"].to<JsonObject>();
        device["clientId"] = config.clientId;
        device["deviceType"] = config.deviceType;
        device["friendlyName"] = config.friendlyName;
        device["commandListenerPort"] = config.commandListenerPort;
        
        // Intervals section
        JsonObject intervals = data["intervals"].to<JsonObject>();
        intervals["heartbeatSeconds"] = config.heartbeatSeconds;
        
        // Gateway section (ESP32 doesn't have this in config, but include for consistency)
        JsonObject gateway = data["gateway"].to<JsonObject>();
        gateway["discoveryTimeoutSeconds"] = 8;  // Default timeout
        
        // Relays section
        JsonArray relays = data["relays"].to<JsonArray>();
        for (int i = 0; i < config.relayCount; i++)
        {
            JsonObject relay = relays.add<JsonObject>();
            relay["id"] = config.relays[i].id;
            relay["name"] = config.relays[i].name;
            relay["pin"] = config.relays[i].pin;
            relay["initialState"] = config.relays[i].initialState;
        }
        
        // Sensors section
        JsonArray sensors = data["sensors"].to<JsonArray>();
        for (int i = 0; i < config.sensorCount; i++)
        {
            JsonObject sensor = sensors.add<JsonObject>();
            sensor["id"] = config.sensors[i].id;
            sensor["type"] = config.sensors[i].type;
            sensor["name"] = config.sensors[i].name;
            sensor["i2cAddress"] = config.sensors[i].i2cAddress;
            sensor["enabled"] = config.sensors[i].enabled;
            sensor["readingIntervalSeconds"] = config.sensors[i].readingIntervalSeconds;
        }
        
        // Buttons section
        JsonArray buttons = data["buttons"].to<JsonArray>();
        for (int i = 0; i < config.buttonCount; i++)
        {
            JsonObject button = buttons.add<JsonObject>();
            button["id"] = config.buttons[i].id;
            button["name"] = config.buttons[i].name;
            button["pin"] = config.buttons[i].pin;
            button["targetDevice"] = config.buttons[i].targetDevice;
            button["targetRelay"] = config.buttons[i].targetRelay;
            button["enabled"] = config.buttons[i].enabled;
        }
        
        logLine("[CommandListener] GetConfig command received, returning configuration");
    }
    else if (strcmp(commandType, "getConfigRaw") == 0)
    {
        // Return raw config.json file content as string (for editing)
        auto configFile = LittleFS.open("/config.json", "r");
        if (!configFile)
        {
            respDoc["success"] = false;
            respDoc["message"] = "Failed to open config.json";
            respDoc["errorCode"] = "FILE_ERROR";
            logLine("[CommandListener] ERROR: Failed to open /config.json");
        }
        else
        {
            String configJson = configFile.readString();
            configFile.close();
            
            respDoc["success"] = true;
            respDoc["message"] = "Configuration file retrieved";
            
            JsonObject data = respDoc["data"].to<JsonObject>();
            data["configJson"] = configJson;
            
            logLine("[CommandListener] GetConfigRaw command received, returning config.json (" + String(configJson.length()) + " bytes)");
        }
    }
    else if (strcmp(commandType, "setConfig") == 0)
    {
        // Write raw config.json file content (for editing)
        JsonObject payload = cmdDoc["payload"];
        
        if (!payload)
        {
            respDoc["success"] = false;
            respDoc["message"] = "Missing payload";
            respDoc["errorCode"] = "MISSING_PAYLOAD";
        }
        else
        {
            const char* configJson = payload["configJson"];
            
            if (!configJson)
            {
                respDoc["success"] = false;
                respDoc["message"] = "Invalid payload: configJson required";
                respDoc["errorCode"] = "INVALID_PAYLOAD";
            }
            else
            {
                // Write to file
                auto configFile = LittleFS.open("/config.json", "w");
                if (!configFile)
                {
                    respDoc["success"] = false;
                    respDoc["message"] = "Failed to open config.json for writing";
                    respDoc["errorCode"] = "FILE_ERROR";
                    logLine("[CommandListener] ERROR: Failed to open /config.json for writing");
                }
                else
                {
                    configFile.print(configJson);
                    configFile.close();
                    
                    respDoc["success"] = true;
                    respDoc["message"] = "Configuration file updated. Reboot required for changes to take effect.";
                    
                    logLine("[CommandListener] SetConfig command received, config.json updated (" + String(strlen(configJson)) + " bytes)");
                }
            }
        }
    }
    else
    {
        respDoc["success"] = false;
        respDoc["message"] = String("Unknown command type: ") + commandType;
        respDoc["errorCode"] = "UNKNOWN_COMMAND";
    }
    
    // Send response
    String response;
    serializeJson(respDoc, response);
    client.println(response);
    client.flush();
    
    logLine("[CommandListener] Sent: " + response);
    
    // Close connection
    client.stop();
}

void stopCommandListener()
{
    if (g_commandServer)
    {
        g_commandServer->stop();
        delete g_commandServer;
        g_commandServer = nullptr;
    }
    
    g_listenerInitialized = false;
    logLine("Command listener stopped");
}
