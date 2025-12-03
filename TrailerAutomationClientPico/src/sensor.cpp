#include "sensor.h"

#include <Arduino.h>
#include <WiFi.h>
#include <HTTPClient.h>
#include <Wire.h>
#include <Adafruit_SHT31.h>

#include "config.h"
#include "logging.h"
#include "network.h"
#include "fsconfig.h"

static Adafruit_SHT31 sht31 = Adafruit_SHT31();
static bool sensorInitialized = false;

bool isSensorAvailable()
{
    return sensorInitialized;
}

bool sendSensorReading()
{
    // Don't try to send if no sensor is initialized
    if (!sensorInitialized)
    {
        return false;
    }
    
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

    // Get device configuration
    const DeviceConfig& config = getDeviceConfig();

    // JSON payload must match TrailerAutomationClientNet:
    // { "ClientId": "...", "TemperatureC": 23.45, "HumidityPercent": 41.23 }
    String payload;
    payload.reserve(128);
    payload  = "{\"ClientId\":\"";
    payload += config.clientId;
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
    const DeviceConfig& config = getDeviceConfig();
    
    // If no sensors configured at all, skip initialization
    if (config.sensorCount == 0)
    {
        logLine("[Sensor] No sensors configured. Sensor readings disabled.");
        sensorInitialized = false;
        return;
    }
    
    // Check if any SHT31 sensors are configured and enabled
    bool hasSht31 = false;
    for (int i = 0; i < config.sensorCount; i++)
    {
        const SensorConfig& s = config.sensors[i];
        if (strcmp(s.type, "SHT31") == 0 && s.enabled)
        {
            hasSht31 = true;
            logLine("Found enabled SHT31 sensor in config: [" + String(s.id) + "] " + String(s.name));
            break;
        }
    }
    
    if (!hasSht31)
    {
        logLine("[Sensor] No enabled SHT31 sensors in config. Sensor readings disabled.");
        sensorInitialized = false;
        return;
    }
    
    logLine("Initializing SHT31 sensor...");
    
    // Initialize I2C with default Pico pins (GP4=SDA, GP5=SCL)
    Wire.setSDA(4);
    Wire.setSCL(5);
    Wire.begin();
    
    if (!sht31.begin(0x44))  // Default I2C address is 0x44
    {
        logLine("ERROR: Could not find SHT31 sensor!");
        logLine("Check wiring: SDA=GP4, SCL=GP5, VCC, GND");
        sensorInitialized = false;
    }
    else
    {
        logLine("SHT31 sensor initialized successfully.");
        sensorInitialized = true;
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
