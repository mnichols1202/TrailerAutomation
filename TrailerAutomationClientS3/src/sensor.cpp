#include "sensor.h"

#include <Arduino.h>
#include <WiFi.h>
#include <HTTPClient.h>

#include "config.h"
#include "logging.h"
#include "network.h"

bool sendSensorReading()
{
    if (!isGatewayKnown())
    {
        logLine("sendSensorReading() called but gateway is unknown.");
        return false;
    }

    if (WiFi.status() != WL_CONNECTED)
    {
        logLine("sendSensorReading() called but Wi-Fi is not connected.");
        return false;
    }

    // Build URL: http://<gatewayHost>:<port>/api/sensor-readings
    String url = String("http://") + getGatewayHost() + ":" +
                 String(getGatewayPort()) + "/api/sensor-readings";

    // Read temperature and humidity (stub for now)
    float temperatureC   = 0.0f;
    float humidityPercent = 0.0f;
    readTemperatureAndHumidity(temperatureC, humidityPercent);

    logLine(String("Sensor reading: TempC=") + String(temperatureC, 2) +
            " Humidity=" + String(humidityPercent, 2));

    // JSON payload must match TrailerAutomationClientNet:
    // { "ClientId": "...", "TemperatureC": 23.45, "HumidityPercent": 41.23 }
    String payload;
    payload.reserve(128);
    payload  = "{\"ClientId\":\"";
    payload += CLIENT_ID;
    payload += "\",\"TemperatureC\":";
    payload += String(temperatureC, 2);
    payload += ",\"HumidityPercent\":";
    payload += String(humidityPercent, 2);
    payload += "}";

    logLine(String("Sending sensor reading to ") + url);
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

    Serial.print("Sensor response code: ");
    Serial.println(httpCode);
    Serial.print("Sensor response body: ");
    Serial.println(response);

    if (httpCode < 200 || httpCode >= 300)
    {
        logLine("Sensor endpoint returned non-success HTTP status.");
        return false;
    }

    return true;
}

// Stub implementation – replace with real sensor later.
void readTemperatureAndHumidity(float& temperatureC, float& humidityPercent)
{
    // Simple plausible dummy values with slight drift so you can see changes.
    static float baseTemp = 22.5f;
    static float baseHum  = 40.0f;

    baseTemp += 0.05f;
    if (baseTemp > 25.0f)
    {
        baseTemp = 22.5f;
    }

    baseHum += 0.10f;
    if (baseHum > 45.0f)
    {
        baseHum = 40.0f;
    }

    temperatureC    = baseTemp;
    humidityPercent = baseHum;
}
