#include "sensor.h"

#include <Arduino.h>
#include <WiFi.h>
#include <HTTPClient.h>
#include <Wire.h>
#include <Adafruit_SHT31.h>

#include "config.h"
#include "logging.h"
#include "network.h"

Adafruit_SHT31 sht31 = Adafruit_SHT31();

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

    // Read temperature and humidity from sensor
    float temperatureC   = 0.0f;
    float humidityPercent = 0.0f;
    if (!readTemperatureAndHumidity(temperatureC, humidityPercent))
    {
        logLine("Failed to read sensor, skipping send.");
        return false;
    }

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

void initSensor()
{
    logLine("Initializing SHT31 sensor...");
    
    if (!sht31.begin(0x44))  // Default I2C address is 0x44
    {
        logLine("ERROR: Could not find SHT31 sensor!");
        logLine("Check wiring: SDA, SCL, VCC, GND");
    }
    else
    {
        logLine("SHT31 sensor initialized successfully.");
    }
}

bool readTemperatureAndHumidity(float& temperatureC, float& humidityPercent)
{
    temperatureC = sht31.readTemperature();
    humidityPercent = sht31.readHumidity();

    // Check if readings are valid
    if (isnan(temperatureC) || isnan(humidityPercent))
    {
        logLine("ERROR: Failed to read from SHT31 sensor!");
        temperatureC = 0.0f;
        humidityPercent = 0.0f;
        return false;
    }
    return true;
}
