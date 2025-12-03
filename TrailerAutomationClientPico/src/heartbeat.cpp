#include "heartbeat.h"

#include <Arduino.h>
#include <WiFi.h>
#include <HTTPClient.h>
#include <ArduinoJson.h>

#include "config.h"
#include "logging.h"
#include "network.h"
#include "fsconfig.h"

bool sendHeartbeat()
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

    // JSON payload mirroring TrailerAutomationClientNet:
    // { "ClientId": "...", "DeviceType": "...", "FriendlyName": "..." }
    String payload;
    payload.reserve(128);
    payload  = "{\"ClientId\":\"";
    payload += config.clientId;
    payload += "\",\"DeviceType\":\"";
    payload += config.deviceType;
    payload += "\",\"FriendlyName\":\"";
    payload += config.friendlyName;
    payload += "\"}";

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
    JsonDocument doc;
    DeserializationError error = deserializeJson(doc, response);
    
    if (error)
    {
        logLine("Failed to parse heartbeat response JSON");
        return false;  // Assume no re-registration if can't parse
    }

    bool needsRegistration = doc["needsRegistration"] | false;
    
    if (needsRegistration)
    {
        logLine("Gateway requests re-registration");
        return true;  // Signal re-registration needed
    }

    return false;  // Heartbeat OK, no re-registration needed
}
