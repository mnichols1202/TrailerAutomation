#include "relaycontrol.h"
#include "sdconfig.h"
#include "logging.h"

bool initRelayControl()
{
    if (!isSdConfigLoaded())
    {
        logLine("ERROR: Cannot init relays - config not loaded");
        return false;
    }
    
    const DeviceConfig& config = getDeviceConfig();
    
    logLine("Initializing " + String(config.relayCount) + " relay(s)...");
    
    for (int i = 0; i < config.relayCount; i++)
    {
        const RelayConfig& relay = config.relays[i];
        
        if (!relay.enabled)
        {
            logLine("  [" + String(relay.id) + "] DISABLED - skipping");
            continue;
        }
        
        // Configure pin as output
        pinMode(relay.pin, OUTPUT);
        
        // Set initial state
        digitalWrite(relay.pin, relay.initialState ? HIGH : LOW);
        
        logLine("  [" + String(relay.id) + "] " + String(relay.name) + 
                " on pin " + String(relay.pin) + 
                " -> " + String(relay.initialState ? "ON" : "OFF"));
    }
    
    logLine("Relay controller initialized");
    return true;
}

bool setRelayState(const char* relayId, bool state)
{
    if (!isSdConfigLoaded())
    {
        return false;
    }
    
    const DeviceConfig& config = getDeviceConfig();
    
    // Find relay by ID
    for (int i = 0; i < config.relayCount; i++)
    {
        const RelayConfig& relay = config.relays[i];
        
        if (strcmp(relay.id, relayId) == 0)
        {
            if (!relay.enabled)
            {
                logLine("Relay [" + String(relayId) + "] is disabled");
                return false;
            }
            
            // Set GPIO pin
            digitalWrite(relay.pin, state ? HIGH : LOW);
            
            logLine("Relay [" + String(relayId) + "] " + String(relay.name) + 
                    " on pin " + String(relay.pin) + 
                    " -> " + String(state ? "ON" : "OFF"));
            
            return true;
        }
    }
    
    logLine("Relay [" + String(relayId) + "] not found");
    return false;
}

bool getRelayState(const char* relayId, bool* outState)
{
    if (!isSdConfigLoaded() || !outState)
    {
        return false;
    }
    
    const DeviceConfig& config = getDeviceConfig();
    
    // Find relay by ID
    for (int i = 0; i < config.relayCount; i++)
    {
        const RelayConfig& relay = config.relays[i];
        
        if (strcmp(relay.id, relayId) == 0)
        {
            if (!relay.enabled)
            {
                return false;
            }
            
            // Read GPIO pin
            *outState = digitalRead(relay.pin) == HIGH;
            return true;
        }
    }
    
    return false;
}

void turnOffAllRelays()
{
    if (!isSdConfigLoaded())
    {
        return;
    }
    
    const DeviceConfig& config = getDeviceConfig();
    
    logLine("Turning off all relays...");
    
    for (int i = 0; i < config.relayCount; i++)
    {
        const RelayConfig& relay = config.relays[i];
        
        if (relay.enabled)
        {
            digitalWrite(relay.pin, LOW);
            logLine("  [" + String(relay.id) + "] -> OFF");
        }
    }
}

String getAllRelayStatesJson()
{
    if (!isSdConfigLoaded())
    {
        return "";
    }
    
    const DeviceConfig& config = getDeviceConfig();
    String result = "";
    
    for (int i = 0; i < config.relayCount; i++)
    {
        const RelayConfig& relay = config.relays[i];
        
        if (!relay.enabled)
        {
            continue;
        }
        
        // Add comma separator if not first entry
        if (result.length() > 0)
        {
            result += ",";
        }
        
        // Read current GPIO state
        bool state = digitalRead(relay.pin) == HIGH;
        
        // Add JSON key-value: "relayId":"on" or "relayId":"off"
        result += "\"";
        result += relay.id;
        result += "\":\"";
        result += (state ? "on" : "off");
        result += "\"";
    }
    
    return result;
}
