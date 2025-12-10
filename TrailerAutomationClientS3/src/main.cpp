#include <Arduino.h>

#include "config.h"
#include "logging.h"
#include "network.h"
#include "heartbeat.h"
#include "sensor.h"
#include "rgbled.h"
#include "power.h"
#include "sdconfig.h"
#include "relaycontrol.h"
#include "commandlistener.h"
#include "button.h"

// Timers for periodic tasks
static unsigned long g_lastHeartbeatMs = 0;
static unsigned long g_lastSensorMs[MAX_SENSORS];  // Per-sensor timing - initialized in setup()
static unsigned long g_bootDelayStartMs = 0;
static bool g_bootDelayComplete = false;
bool g_deviceRegistered = false;  // Non-static so commandlistener can access

// Gateway reconnection tracking
static int g_consecutiveHeartbeatFailures = 0;
static const int MAX_FAILURES_BEFORE_REDISCOVERY = 3;

// Intervals from config (will be loaded from LittleFS)
static unsigned long g_heartbeatIntervalMs = 60000UL; // Default 60s

void setup()
{
    Serial.begin(921600);
    delay(100);  // Let serial stabilize
    
    // Initialize RGB LED and show red boot indicator
    initRgbLed();
    setLedState(LED_BOOT);
    
    logLine("TrailerAutomationClientS3 starting...");
    
    // Initialize LittleFS and load configuration FIRST
    if (!initSdConfig())
    {
        logLine("FATAL: Failed to load LittleFS configuration!");
        logLine("System halted. Please check LittleFS and config.json.");
        setLedError(ERROR_SENSOR_READ); // Blink red to indicate config error
        while (1)
        {
            updateLed();
            delay(100);
        }
    }
    
    // Load heartbeat interval from config
    const DeviceConfig& config = getDeviceConfig();
    g_heartbeatIntervalMs = config.heartbeatSeconds * 1000UL;
    // Sensor intervals are now per-sensor in config.sensors[].readingIntervalSeconds
    
    // Initialize sensor timers to trigger first reading immediately after initialization
    // Set each sensor's timer based on its configured interval to ensure immediate first reading
    for (int i = 0; i < config.sensorCount && i < MAX_SENSORS; i++)
    {
        unsigned long intervalMs = config.sensors[i].readingIntervalSeconds * 1000UL;
        // Set to current time minus interval so first check will trigger immediately
        g_lastSensorMs[i] = millis() - intervalMs;
    }
    
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
    
    // Initialize relay control from config
    if (!initRelayControl())
    {
        logLine("WARNING: Relay control initialization failed");
    }
    
    // Initialize buttons from config
    if (!initButtons())
    {
        logLine("WARNING: Button initialization failed");
    }
    
    // Note: WiFi connection will happen in loop() after boot delay
}

void loop()
{
    // Update LED animation
    updateLed();
    
    // Check if boot delay is complete
    if (!g_bootDelayComplete)
    {
        if (millis() - g_bootDelayStartMs >= 3000)
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
            // Initialize sensor timers to 0 so first reading happens immediately
            for (int i = 0; i < MAX_SENSORS; i++)
            {
                g_lastSensorMs[i] = 0;
            }
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

        logLine("Gateway discovered, will start initialization sequence.");
        
        // Send immediate heartbeat
        if (!sendHeartbeat())
        {
            logLine("Initial heartbeat failed, will retry in loop");
        }
        
        // Register device (includes relay config and command port)
        if (!registerDevice())
        {
            logLine("Device registration failed, will retry in loop");
        }
        else
        {
            g_deviceRegistered = true;
            
            // Start command listener
            if (!initCommandListener())
            {
                logLine("Command listener failed to start");
            }
        }
        
        clearLedError();  // Clear any previous errors
        setLedState(LED_CONNECTED);  // Show green for 5 seconds, then turn off
    }

    // 3. Re-register if needed (gateway might have restarted)
    if (isGatewayKnown() && !g_deviceRegistered)
    {
        if (registerDevice())
        {
            g_deviceRegistered = true;
            
            // Start command listener if not already running
            if (!initCommandListener())
            {
                logLine("Command listener failed to start");
            }
        }
    }

    // 4. Process incoming commands (non-blocking)
    processCommandListener();
    
    // 5. Check buttons for state changes
    checkButtons();

    // 6. Heartbeat timing
    if (now - g_lastHeartbeatMs >= g_heartbeatIntervalMs)
    {
        // sendHeartbeat() returns true on success
        // Pass g_deviceRegistered so it knows whether to sync relay states on re-registration
        bool heartbeatSuccess = sendHeartbeat(g_deviceRegistered);
        
        if (heartbeatSuccess)
        {
            // Heartbeat succeeded - reset failure counter
            g_consecutiveHeartbeatFailures = 0;
            consecutiveFailures = 0;
            lastSuccessfulComm = now;
            clearLedError();
        }
        else
        {
            // Heartbeat failed
            g_consecutiveHeartbeatFailures++;
            consecutiveFailures++;
            logLine("Heartbeat failed (" + String(g_consecutiveHeartbeatFailures) + "/" + 
                    String(MAX_FAILURES_BEFORE_REDISCOVERY) + ")");
            
            if (g_consecutiveHeartbeatFailures >= MAX_FAILURES_BEFORE_REDISCOVERY)
            {
                logLine("Gateway unreachable - attempting mDNS re-discovery...");
                forgetGateway();  // Clear cached Gateway info
                g_deviceRegistered = false;
                g_consecutiveHeartbeatFailures = 0;  // Reset to avoid constant rediscovery
                
                // Next loop iteration will trigger discovery in step 2
                setLedError(ERROR_MDNS);
            }
        }
        
        // Check if gateway requests re-registration (handled inside sendHeartbeat now)
        if (!g_deviceRegistered && isGatewayKnown())
        {
            // Re-registration needed
            if (registerDevice())
            {
                g_deviceRegistered = true;
                logLine("Re-registration successful");
                
                // Restart command listener if needed
                if (!initCommandListener())
                {
                    logLine("Command listener failed to restart");
                }
            }
        }
        
        g_lastHeartbeatMs = now;
    }

    // 7. Sensor timing (independent of heartbeat) - check each sensor independently
    if (isSensorAvailable())
    {
        const DeviceConfig& config = getDeviceConfig();
        
        for (int i = 0; i < config.sensorCount; i++)
        {
            const SensorConfig& sensor = config.sensors[i];
            
            if (!sensor.enabled)
                continue;
                
            unsigned long intervalMs = sensor.readingIntervalSeconds * 1000UL;
            
            if (now - g_lastSensorMs[i] >= intervalMs)
            {
                if (!sendSensorReading())
                {
                    logLine("[" + String(sensor.id) + "] Sensor reading failed. Will keep trying.");
                    setLedError(ERROR_SENSOR_SEND);  // 3 red blinks - Sensor send failed
                }
                else
                {
                    logLine("[" + String(sensor.id) + "] Sensor reading sent");
                    clearLedError();  // Clear error if sensor send succeeds
                }
                g_lastSensorMs[i] = now;
            }
        }
    }

    // 7. Check button states
    checkButtons();

    // 8. Small delay to avoid busy spin
    delay(10);
}
