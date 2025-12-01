#include <Arduino.h>

#include "config.h"
#include "logging.h"
#include "network.h"
#include "heartbeat.h"
#include "sensor.h"
#include "rgbled.h"
#include "power.h"

// Timers for periodic tasks
static unsigned long g_lastHeartbeatMs = 0;
static unsigned long g_lastSensorMs    = 0;
static unsigned long g_bootDelayStartMs = 0;
static bool g_bootDelayComplete = false;

// Sensor report interval (milliseconds)
// Separate from HEARTBEAT_INTERVAL_MS which comes from config.h
static const unsigned long SENSOR_INTERVAL_MS = 30UL * 1000UL; // 30 seconds

void setup()
{
    Serial.begin(921600);
    delay(100);  // Let serial stabilize
    
    // Initialize RGB LED and show red boot indicator
    initRgbLed();
    setLedState(LED_BOOT);
    
    logLine("TrailerAutomationClientS3 starting...");
    logLine("Starting 15-second boot delay for hardware initialization (battery-safe)...");
    
    // Start non-blocking boot delay timer
    g_bootDelayStartMs = millis();
    g_bootDelayComplete = false;
    
    // CRITICAL: Disable WiFi radio immediately to save power and ensure clean state
    // This is especially important for battery-powered operation
    WiFi.persistent(false);  // Don't save WiFi config to flash
    WiFi.mode(WIFI_OFF);
    delay(100);
    
    // Initialize power monitoring
    initPowerMonitoring();
    
    // Initialize SHT31 sensor (can happen during boot delay)
    initSensor();
    
    // Note: WiFi connection will happen in loop() after boot delay
}

void loop()
{
    // Update LED animation
    updateLed();
    
    // Check if boot delay is complete
    if (!g_bootDelayComplete)
    {
        if (millis() - g_bootDelayStartMs >= 15000)
        {
            logLine("Boot delay complete. Initializing WiFi radio...");
            
            // Power up WiFi radio with explicit sequence for battery operation
            WiFi.mode(WIFI_STA);
            delay(1000);  // Longer delay for radio to stabilize on battery power
            
            g_bootDelayComplete = true;
            
            // Show connecting status (solid blue)
            setLedState(LED_CONNECTING);
            
            // Attempt initial WiFi connection
            if (!ensureWifiConnected())
            {
                logLine("Initial Wi-Fi connection failed, will retry in loop.");
            }
            
            // Start mDNS
            if (!startMdns())
            {
                logLine("mDNS startup failed, gateway discovery may not work.");
                setLedError(ERROR_MDNS);
            }
            else
            {
                logLine("mDNS initialized, will attempt gateway discovery.");
            }
            
            unsigned long now = millis();
            g_lastHeartbeatMs = now;
            g_lastSensorMs    = now;
        }
        
        // Still waiting for boot delay - keep LED red and yield
        delay(100);
        return;
    }
    
    // 1. Ensure Wi-Fi
    // Check for "zombie" WiFi - connected but no actual traffic
    static unsigned long lastSuccessfulComm = millis();
    static int consecutiveFailures = 0;
    
    if (!ensureWifiConnected())
    {
        consecutiveFailures++;
        
        if (isGatewayKnown())
        {
            logLine("Lost Wi-Fi; forgetting previously known gateway.");
            forgetGateway();
        }

        // Show connecting status (solid blue)
        setLedState(LED_CONNECTING);

        // Determine specific WiFi error code
        int wifiStatus = getLastWifiError();
        LedErrorCode errorCode;
        
        switch (wifiStatus)
        {
            case WL_NO_SSID_AVAIL:
                errorCode = ERROR_WIFI_NO_SSID;  // 2 blue blinks
                break;
            case WL_CONNECT_FAILED:
                errorCode = ERROR_WIFI_AUTH_FAIL;  // 3 blue blinks
                break;
            case WL_DISCONNECTED:
                errorCode = ERROR_WIFI_DISCONNECTED;  // 4 blue blinks
                break;
            case WL_IDLE_STATUS:
                errorCode = ERROR_WIFI_TIMEOUT;  // 1 blue blink
                break;
            default:
                errorCode = ERROR_WIFI_GENERIC;  // 5 blue blinks
                break;
        }
        
        setLedError(errorCode);
        delay(1000);
        return;
    }

    // 2. Ensure gateway discovery
    unsigned long now = millis();
    
    if (!isGatewayKnown())
    {
        logLine("Gateway not known, attempting mDNS discovery...");
        if (!discoverGateway())
        {
            logLine("Gateway discovery attempt failed; will retry.");
            setLedError(ERROR_MDNS);  // 1 red blink - mDNS/Gateway discovery failed
            delay(2000);
            return;
        }

        logLine("Gateway discovered, will start heartbeat and sensor loops.");
        clearLedError();  // Clear any previous errors
        setLedState(LED_CONNECTED);  // Show green for 5 seconds, then turn off
    }

    // 3. Heartbeat timing
    if (now - g_lastHeartbeatMs >= HEARTBEAT_INTERVAL_MS)
    {
        if (!sendHeartbeat())
        {
            logLine("Heartbeat failed. Will keep trying.");
            setLedError(ERROR_HEARTBEAT);  // 2 red blinks - Heartbeat failed
            consecutiveFailures++;
            
            // Zombie WiFi detection - connected but traffic failing
            if (consecutiveFailures >= 3 && WiFi.status() == WL_CONNECTED)
            {
                logLine("WARNING: Zombie WiFi detected (connected but no traffic). Forcing reconnect...");
                WiFi.disconnect(true);
                delay(1000);
                consecutiveFailures = 0;
                return;  // Will reconnect on next loop
            }
        }
        else
        {
            clearLedError();  // Clear error if heartbeat succeeds
            consecutiveFailures = 0;  // Reset failure counter on success
            lastSuccessfulComm = now;
        }
        g_lastHeartbeatMs = now;
    }

    // 4. Sensor timing (independent of heartbeat)
    if (now - g_lastSensorMs >= SENSOR_INTERVAL_MS)
    {
        if (!sendSensorReading())
        {
            logLine("Sensor reading failed. Will keep trying.");
            setLedError(ERROR_SENSOR_SEND);  // 3 red blinks - Sensor send failed
        }
        else
        {
            clearLedError();  // Clear error if sensor send succeeds
        }
        g_lastSensorMs = now;
    }

    // 5. Small delay to avoid busy spin
    delay(10);
}
