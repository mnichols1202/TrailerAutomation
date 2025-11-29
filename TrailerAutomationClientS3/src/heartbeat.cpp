#include "heartbeat.h"

#include <Arduino.h>
#include <WiFi.h>
#include <HTTPClient.h>

#include "config.h"
#include "logging.h"
#include "network.h"

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

    // Build URL: http://<gatewayHost>:<port>/api/heartbeat
    String url = String("http://") + getGatewayHost() + ":" + String(getGatewayPort()) + "/api/heartbeat";

    // JSON payload mirroring TrailerAutomationClientNet:
    // { "ClientId": "...", "DeviceType": "...", "FriendlyName": "..." }
    String payload;
    payload.reserve(128);
    payload  = "{\"ClientId\":\"";
    payload += CLIENT_ID;
    payload += "\",\"DeviceType\":\"";
    payload += DEVICE_TYPE;
    payload += "\",\"FriendlyName\":\"";
    payload += FRIENDLY_NAME;
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
        return false;
    }

    return true;
}
