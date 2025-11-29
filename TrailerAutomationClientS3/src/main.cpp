#include <Arduino.h>

#include "config.h"
#include "logging.h"
#include "network.h"
#include "heartbeat.h"
#include "sensor.h"

// Timers for periodic tasks
static unsigned long g_lastHeartbeatMs = 0;
static unsigned long g_lastSensorMs    = 0;

// Sensor report interval (milliseconds)
// Separate from HEARTBEAT_INTERVAL_MS which comes from config.h
static const unsigned long SENSOR_INTERVAL_MS = 30UL * 1000UL; // 30 seconds

void setup()
{
    Serial.begin(115200);
    delay(1000);

    logLine("TrailerAutomationClientS3 starting...");
    
    // Initialize SHT31 sensor
    initSensor();
    
    logLine("Initializing Wi-Fi...");

    // Initial Wi-Fi connect
    if (!ensureWifiConnected())
    {
        logLine("Initial Wi-Fi connection failed, will retry in loop.");
    }

    // Start mDNS
    if (!startMdns())
    {
        logLine("mDNS startup failed, gateway discovery may not work.");
    }
    else
    {
        logLine("mDNS initialized, will attempt gateway discovery in loop.");
    }

    unsigned long now = millis();
    g_lastHeartbeatMs = now;
    g_lastSensorMs    = now;
}

void loop()
{
    // 1. Ensure Wi-Fi
    if (!ensureWifiConnected())
    {
        if (isGatewayKnown())
        {
            logLine("Lost Wi-Fi; forgetting previously known gateway.");
            forgetGateway();
        }

        delay(1000);
        return;
    }

    // 2. Ensure gateway discovery
    if (!isGatewayKnown())
    {
        logLine("Gateway not known, attempting mDNS discovery...");
        if (!discoverGateway())
        {
            logLine("Gateway discovery attempt failed; will retry.");
            delay(2000);
            return;
        }

        logLine("Gateway discovered, will start heartbeat and sensor loops.");
    }

    // 3. Heartbeat timing
    unsigned long now = millis();
    if (now - g_lastHeartbeatMs >= HEARTBEAT_INTERVAL_MS)
    {
        if (!sendHeartbeat())
        {
            logLine("Heartbeat failed. Will keep trying.");
        }
        g_lastHeartbeatMs = now;
    }

    // 4. Sensor timing (independent of heartbeat)
    if (now - g_lastSensorMs >= SENSOR_INTERVAL_MS)
    {
        if (!sendSensorReading())
        {
            logLine("Sensor reading failed. Will keep trying.");
        }
        g_lastSensorMs = now;
    }

    // 5. Small delay to avoid busy spin
    delay(10);
}
