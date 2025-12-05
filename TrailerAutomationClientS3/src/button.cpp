#include "button.h"
#include "sdconfig.h"
#include "relaycontrol.h"
#include "logging.h"
#include "network.h"
#include <WiFi.h>
#include <HTTPClient.h>
#include <ArduinoJson.h>

// Button state tracking
struct ButtonState
{
    int pin;
    bool lastState;
    unsigned long lastDebounceTime;
    char targetDevice[MAX_DEVICE_ID_LEN];
    char targetRelay[MAX_RELAY_ID_LEN];
    bool relayState;  // Track relay state locally like web UI
};

static ButtonState g_buttonStates[MAX_BUTTONS];
static int g_buttonCount = 0;
static bool g_buttonsInitialized = false;

#define DEBOUNCE_DELAY_MS 50

bool initButtons()
{
    if (!isSdConfigLoaded())
    {
        logLine("ERROR: Cannot initialize buttons - config not loaded");
        return false;
    }
    
    const DeviceConfig& config = getDeviceConfig();
    g_buttonCount = config.buttonCount;
    
    if (g_buttonCount == 0)
    {
        logLine("No buttons configured");
        g_buttonsInitialized = true;
        return true;
    }
    
    logLine("Initializing " + String(g_buttonCount) + " button(s)...");
    
    for (int i = 0; i < g_buttonCount; i++)
    {
        const ButtonConfig& btn = config.buttons[i];
        
        if (!btn.enabled)
        {
            logLine("Button '" + String(btn.name) + "' disabled, skipping");
            continue;
        }
        
        // Configure pin as input with pull-up (active LOW)
        pinMode(btn.pin, INPUT_PULLUP);
        
        // Initialize state tracking
        g_buttonStates[i].pin = btn.pin;
        g_buttonStates[i].lastState = HIGH; // Pull-up = HIGH when not pressed
        g_buttonStates[i].lastDebounceTime = 0;
        strncpy(g_buttonStates[i].targetDevice, btn.targetDevice, MAX_DEVICE_ID_LEN - 1);
        strncpy(g_buttonStates[i].targetRelay, btn.targetRelay, MAX_RELAY_ID_LEN - 1);
        g_buttonStates[i].relayState = false; // Assume relay starts off
        
        logLine("Button '" + String(btn.name) + "' initialized on pin " + String(btn.pin) + 
                " -> " + String(btn.targetDevice) + ":" + String(btn.targetRelay));
    }
    
    g_buttonsInitialized = true;
    logLine("Button initialization complete");
    return true;
}

static void handleLocalToggle(ButtonState* btnState)
{
    // Flip local state tracking (like web UI does)
    btnState->relayState = !btnState->relayState;
    
    if (setRelayState(btnState->targetRelay, btnState->relayState))
    {
        logLine("[Button] Set local relay '" + String(btnState->targetRelay) + "' to " + (btnState->relayState ? "ON" : "OFF"));
        
        // Notify gateway of state change so web UI updates
        const char* stateStr = btnState->relayState ? "on" : "off";
        const DeviceConfig& config = getDeviceConfig();
        
        if (isGatewayKnown())
        {
            String url = "http://" + getGatewayHost() + ":" + String(getGatewayPort()) + 
                         "/api/devices/" + String(config.clientId) + 
                         "/relays/" + String(btnState->targetRelay) + "/state?state=" + stateStr;
            
            HTTPClient http;
            http.begin(url);
            http.setTimeout(5000);
            
            logLine("[Button] Notifying gateway of relay state change");
            
            int httpCode = http.POST("");
            
            if (httpCode == HTTP_CODE_OK)
            {
                logLine("[Button] Gateway notified successfully");
            }
            else
            {
                logLine("[Button] WARNING: Gateway notification failed, HTTP code " + String(httpCode));
            }
            
            http.end();
        }
    }
    else
    {
        logLine("[Button] ERROR: Failed to set relay state for '" + String(btnState->targetRelay) + "'");
        // Revert state on failure
        btnState->relayState = !btnState->relayState;
    }
}

static void handleRemoteToggle(ButtonState* btnState)
{
    // Send explicit state command to gateway (like web UI does)
    if (!isGatewayKnown())
    {
        logLine("[Button] ERROR: Gateway unknown, cannot control remote relay");
        return;
    }
    
    // Flip local state tracking
    btnState->relayState = !btnState->relayState;
    const char* stateStr = btnState->relayState ? "on" : "off";
    
    String url = "http://" + getGatewayHost() + ":" + String(getGatewayPort()) + 
                 "/api/devices/" + String(btnState->targetDevice) + 
                 "/relays/" + String(btnState->targetRelay) + "/state?state=" + stateStr;
    
    HTTPClient http;
    http.begin(url);
    http.setTimeout(5000);
    
    logLine("[Button] Setting remote relay to " + String(stateStr));
    
    int httpCode = http.POST("");
    
    if (httpCode == HTTP_CODE_OK)
    {
        String response = http.getString();
        logLine("[Button] Remote relay set to " + String(stateStr));
    }
    else
    {
        logLine("[Button] ERROR: Remote command failed, HTTP code " + String(httpCode));
        // Revert state on failure
        btnState->relayState = !btnState->relayState;
    }
    
    http.end();
}

void checkButtons()
{
    if (!g_buttonsInitialized || g_buttonCount == 0)
    {
        return;
    }
    
    const DeviceConfig& config = getDeviceConfig();
    unsigned long now = millis();
    
    for (int i = 0; i < g_buttonCount; i++)
    {
        const ButtonConfig& btn = config.buttons[i];
        
        if (!btn.enabled)
        {
            continue;
        }
        
        ButtonState& state = g_buttonStates[i];
        
        // Read current button state (active LOW)
        bool currentReading = digitalRead(state.pin);
        
        // Check if state changed (for debouncing)
        if (currentReading != state.lastState)
        {
            state.lastDebounceTime = now;
        }
        
        // If debounce time has passed, accept the reading
        if ((now - state.lastDebounceTime) > DEBOUNCE_DELAY_MS)
        {
            // Button is pressed when LOW (pull-up configuration)
            // Trigger on rising edge (button release) to avoid repeated triggers
            if (state.lastState == LOW && currentReading == HIGH)
            {
                // Button was pressed and now released - toggle!
                logLine("[Button] Button '" + String(btn.name) + "' pressed");
                
                // Check if target is local or remote
                if (strcmp(state.targetDevice, config.clientId) == 0)
                {
                    // Local relay
                    handleLocalToggle(&state);
                }
                else
                {
                    // Remote relay
                    handleRemoteToggle(&state);
                }
            }
        }
        
        state.lastState = currentReading;
    }
}
