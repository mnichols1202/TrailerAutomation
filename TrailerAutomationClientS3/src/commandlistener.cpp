#include "commandlistener.h"
#include "sdconfig.h"
#include "relaycontrol.h"
#include "logging.h"
#include <WiFi.h>
#include <ArduinoJson.h>

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
