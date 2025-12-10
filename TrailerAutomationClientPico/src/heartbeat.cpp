#include "heartbeat.h"

#include <Arduino.h>
#include <WiFi.h>
#include <HTTPClient.h>
#include <ArduinoJson.h>

#include "config.h"
#include "logging.h"
#include "network.h"
#include "fsconfig.h"
#include "relaycontrol.h"

bool sendHeartbeat(bool wasRegistered)
{
    if (!isGatewayKnown())
    {
        logLine("sendHeartbeat() called but gateway is unknown.");
        return false;
    }

    if (WiFi.status() != WL_CONNECTED)
    {
        logLine("sendHeartbeat() called but Wi-Fi is not connected.");
        return false;
    }

    // Get device configuration
    const DeviceConfig& config = getDeviceConfig();

    // Build URL: http://<gatewayHost>:<port>/api/heartbeat
    String url = String("http://") + getGatewayHost() + ":" + String(getGatewayPort()) + "/api/heartbeat";

    // Build JSON payload using ArduinoJson for proper serialization
    JsonDocument doc;
    doc["ClientId"] = config.clientId;
    doc["DeviceType"] = config.deviceType;
    doc["FriendlyName"] = config.friendlyName;
    
    String payload;
    serializeJson(doc, payload);

    logLine(String("Sending heartbeat to ") + url);
    logLine(String("Payload: ") + payload);

    HTTPClient http;
    http.begin(url);
    http.addHeader("Content-Type", "application/json");

    int httpCode = http.POST(reinterpret_cast<uint8_t*>(const_cast<char*>(payload.c_str())), payload.length());

    if (httpCode <= 0)
    {
        logLine(String("HTTP POST failed: ") + http.errorToString(httpCode));
        http.end();
        return false;
    }

    String response = http.getString();
    http.end();

    Serial.print("Heartbeat response code: ");
    Serial.println(httpCode);
    Serial.print("Heartbeat response body: ");
    Serial.println(response);

    if (httpCode < 200 || httpCode >= 300)
    {
        logLine("Heartbeat received non-success HTTP status.");
        return false;  // Return false on HTTP error (not re-registration needed)
    }

    // Parse JSON response to check for "needsRegistration": true
    JsonDocument responseDoc;
    DeserializationError error = deserializeJson(responseDoc, response);
    
    if (error)
    {
        logLine("Failed to parse heartbeat response JSON");
        return false;  // Assume no re-registration if can't parse
    }

    bool needsRegistration = responseDoc["needsRegistration"] | false;
    
    if (needsRegistration)
    {
        // Only sync relay states if this is a RE-registration (was previously registered)
        if (wasRegistered)
        {
            logLine("Gateway requests re-registration - syncing relay states...");
            
            // Send heartbeat WITH relay states to sync after Gateway restart
            JsonDocument syncDoc;
            syncDoc["ClientId"] = config.clientId;
            syncDoc["DeviceType"] = config.deviceType;
            syncDoc["FriendlyName"] = config.friendlyName;
            
            // Add relay states if any
            String relayStates = getAllRelayStatesJson();
            if (relayStates.length() > 0)
            {
                // Parse relay states JSON and add to document
                JsonDocument relayDoc;
                deserializeJson(relayDoc, "{" + relayStates + "}");
                syncDoc["RelayStates"] = relayDoc;
            }
            
            String syncPayload;
            serializeJson(syncDoc, syncPayload);
            
            HTTPClient httpSync;
            httpSync.begin(url);
            httpSync.addHeader("Content-Type", "application/json");
            httpSync.POST(reinterpret_cast<uint8_t*>(const_cast<char*>(syncPayload.c_str())), syncPayload.length());
            httpSync.end();
            
            logLine("Relay states synced - re-registration needed");
        }
        else
        {
            logLine("Gateway requests initial registration (skipping relay state sync)");
        }
        
        // Set global flag for main loop to handle registration
        extern bool g_deviceRegistered;
        g_deviceRegistered = false;
    }

    return true;  // Heartbeat succeeded (may need re-registration, checked via g_deviceRegistered)
}
