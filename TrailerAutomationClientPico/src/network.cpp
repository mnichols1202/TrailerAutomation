#include "network.h"

#include <LEAmDNS.h>

#include "config.h"
#include "logging.h"
#include "fsconfig.h"

// Internal gateway state
static String   g_gatewayHost;
static uint16_t g_gatewayPort  = GATEWAY_DEFAULT_PORT;
static bool     g_gatewayKnown = false;

// -----------------------------------------------------------------------------
// Wi-Fi
// -----------------------------------------------------------------------------

// Global variable to store last WiFi error
static int g_lastWifiError = 0;

// Helper function to get human-readable WiFi status
static String getWiFiStatusString(int status)
{
    switch (status)
    {
        case WL_IDLE_STATUS:     return "IDLE";
        case WL_NO_SSID_AVAIL:   return "NO_SSID_AVAILABLE";
        case WL_SCAN_COMPLETED:  return "SCAN_COMPLETED";
        case WL_CONNECTED:       return "CONNECTED";
        case WL_CONNECT_FAILED:  return "CONNECT_FAILED";
        case WL_CONNECTION_LOST: return "CONNECTION_LOST";
        case WL_DISCONNECTED:    return "DISCONNECTED";
        default:                 return "UNKNOWN(" + String(status) + ")";
    }
}

bool ensureWifiConnected()
{
    if (WiFi.status() == WL_CONNECTED)
    {
        return true;
    }

    logLine("Wi-Fi not connected, attempting to connect...");

    // Get WiFi credentials from config
    const DeviceConfig& config = getDeviceConfig();
    
    logLine("WiFi Configuration:");
    logLine("  SSID: [" + String(config.wifiSSID) + "]");
    logLine("  Password: [" + String(config.wifiPassword) + "]");
    logLine("  SSID Length: " + String(strlen(config.wifiSSID)));
    logLine("  Password Length: " + String(strlen(config.wifiPassword)));
    
    // Print MAC address for router whitelisting
    uint8_t mac[6];
    WiFi.macAddress(mac);
    char macStr[18];
    sprintf(macStr, "%02X:%02X:%02X:%02X:%02X:%02X", mac[0], mac[1], mac[2], mac[3], mac[4], mac[5]);
    logLine("  Pico 2W MAC: " + String(macStr));

    // Try multiple connection attempts with increasing delays
    // Pico 2W doesn't support power level adjustment like ESP32
    for (int attempt = 1; attempt <= 3; attempt++)
    {
        logLine("Connection attempt " + String(attempt) + "/3...");
        
        // Properly disconnect and reset WiFi to clear any bad state
        WiFi.disconnect();
        delay(500);
        WiFi.mode(WIFI_OFF);
        delay(500);
        
        // Start fresh
        WiFi.mode(WIFI_STA);
        delay(1000);
        
        // Use progressively longer delays for unstable connections
        // Lower delays first, increase if connection fails
        unsigned long timeoutMs;
        if (attempt == 1)
        {
            timeoutMs = 15000UL;  // 15s timeout - quick first try
            logLine("  Using SHORT timeout (15s) for quick check");
        }
        else if (attempt == 2)
        {
            timeoutMs = 20000UL;  // 20s timeout - medium
            logLine("  Using MEDIUM timeout (20s)");
        }
        else
        {
            timeoutMs = 30000UL;  // 30s timeout - long final attempt
            logLine("  Using LONG timeout (30s) - final attempt");
        }
        
        // Begin connection
        WiFi.begin(config.wifiSSID, config.wifiPassword);
        
        const unsigned long start = millis();
        int lastStatus = -1;

        while (WiFi.status() != WL_CONNECTED && (millis() - start) < timeoutMs)
        {
            delay(500);
            Serial.print(".");
            
            // Log status changes
            int currentStatus = WiFi.status();
            if (currentStatus != lastStatus)
            {
                Serial.println();
                logLine("  Status changed to: " + String(currentStatus) + " [" + getWiFiStatusString(currentStatus) + "]");
                lastStatus = currentStatus;
            }
        }
        Serial.println();
        
        if (WiFi.status() == WL_CONNECTED)
        {
            logLine("✅ Wi-Fi connected successfully on attempt " + String(attempt));
            Serial.print("  IP address: ");
            Serial.println(WiFi.localIP());
            Serial.print("  Gateway: ");
            Serial.println(WiFi.gatewayIP());
            Serial.print("  DNS: ");
            Serial.println(WiFi.dnsIP());
            Serial.print("  RSSI: ");
            Serial.print(WiFi.RSSI());
            Serial.println(" dBm");
            
            g_lastWifiError = 0;
            return true;
        }
        
        logLine("❌ Attempt " + String(attempt) + " failed with status: " + getWiFiStatusString(WiFi.status()));
        
        if (attempt < 3)
        {
            delay(2000);  // Wait before next attempt
        }
    }
    
    // All attempts failed
    g_lastWifiError = WiFi.status();
    logLine("ERROR: All WiFi connection attempts failed!");
    
    // Log specific error
    switch (WiFi.status())
    {
        case WL_NO_SSID_AVAIL:
            logLine("Wi-Fi error: SSID not found");
            break;
        case WL_CONNECT_FAILED:
            logLine("Wi-Fi error: Connection failed (wrong password?)");
            break;
        case WL_DISCONNECTED:
            logLine("Wi-Fi error: Disconnected");
            break;
        default:
            logLine(String("Wi-Fi connection timeout. Status: ") + String(WiFi.status()));
            break;
    }
    
    return false;
}

int getLastWifiError()
{
    return g_lastWifiError;
}

// -----------------------------------------------------------------------------
// mDNS
// -----------------------------------------------------------------------------

bool startMdns()
{
    // Get device configuration
    const DeviceConfig& config = getDeviceConfig();
    
    // Use ClientId as the mDNS hostname so you can see it on the LAN if desired
    if (!MDNS.begin(config.clientId))
    {
        logLine("MDNS.begin() failed.");
        return false;
    }

    logLine("mDNS responder started.");
    return true;
}

bool discoverGateway()
{
    // Query for the same service type used by TrailerAutomationGateway:
    //
    // C# uses "_trailer-gateway._tcp" (see MdnsHost.cs).
    // LEAmDNS expects service name and protocol WITHOUT leading underscores.
    int n = MDNS.queryService(MDNS_SERVICE_NAME, MDNS_SERVICE_PROTO);

    if (n <= 0)
    {
        logLine("mDNS queryService returned no results.");
        return false;
    }

    // Take the first result
    IPAddress hostIp = MDNS.IP(0);
    uint16_t port    = MDNS.port(0);

    if (!hostIp)
    {
        logLine("mDNS result did not contain a valid IP address.");
        return false;
    }

    g_gatewayHost  = hostIp.toString();
    g_gatewayPort  = (port != 0) ? port : GATEWAY_DEFAULT_PORT;
    g_gatewayKnown = true;

    Serial.print("Gateway discovered at http://");
    Serial.print(g_gatewayHost);
    Serial.print(":");
    Serial.println(g_gatewayPort);

    return true;
}

// -----------------------------------------------------------------------------
// Gateway state accessors
// -----------------------------------------------------------------------------

bool isGatewayKnown()
{
    return g_gatewayKnown;
}

void forgetGateway()
{
    g_gatewayKnown = false;
    g_gatewayHost  = String();
    g_gatewayPort  = GATEWAY_DEFAULT_PORT;
}

String getGatewayHost()
{
    return g_gatewayHost;
}

uint16_t getGatewayPort()
{
    return g_gatewayPort;
}

// -----------------------------------------------------------------------------
// Device Registration
// -----------------------------------------------------------------------------

#include <HTTPClient.h>
#include <ArduinoJson.h>

bool registerDevice()
{
    if (!g_gatewayKnown)
    {
        logLine("[DeviceRegistration] Gateway not known, cannot register");
        return false;
    }
    
    if (!isFsConfigLoaded())
    {
        logLine("[DeviceRegistration] Config not loaded, cannot register");
        return false;
    }
    
    const DeviceConfig& config = getDeviceConfig();
    
    logLine("[DeviceRegistration] Registering with gateway...");
    
    // Build registration JSON
    JsonDocument doc;
    doc["ClientId"] = config.clientId;
    doc["DeviceType"] = config.deviceType;
    doc["FriendlyName"] = config.friendlyName;
    doc["IpAddress"] = WiFi.localIP().toString();
    doc["CommandPort"] = config.commandListenerPort;
    
    // Add capabilities
    JsonArray capabilities = doc["Capabilities"].to<JsonArray>();
    if (config.relayCount > 0)
    {
        capabilities.add("relay");
    }
    
    // Add button capability if buttons are configured
    if (config.buttonCount > 0)
    {
        for (int i = 0; i < config.buttonCount; i++)
        {
            if (config.buttons[i].enabled)
            {
                capabilities.add("button");
                break; // Only add once
            }
        }
    }
    
    // Only add sensor capabilities if sensors are configured and available
    if (config.sensorCount > 0)
    {
        for (int i = 0; i < config.sensorCount; i++)
        {
            const SensorConfig& sensor = config.sensors[i];
            if (sensor.enabled)
            {
                // Add capabilities based on sensor type
                if (strcmp(sensor.type, "SHT31") == 0)
                {
                    capabilities.add("temp");
                    capabilities.add("humidity");
                }
            }
        }
    }
    
    // Add relay info with current states for UI
    if (config.relayCount > 0)
    {
        JsonArray relays = doc["Relays"].to<JsonArray>();
        for (int i = 0; i < config.relayCount; i++)
        {
            const RelayConfig& relay = config.relays[i];
            if (relay.enabled)
            {
                bool state = false;
                getRelayState(relay.id, &state);
                
                JsonObject r = relays.add<JsonObject>();
                r["Id"] = relay.id;
                r["Name"] = relay.name;
                r["State"] = state ? "on" : "off";
            }
        }
    }
    
    String jsonPayload;
    serializeJson(doc, jsonPayload);
    
    // Send POST request
    HTTPClient http;
    String url = "http://" + g_gatewayHost + ":" + String(g_gatewayPort) + "/api/devices/register";
    
    http.begin(url);
    http.addHeader("Content-Type", "application/json");
    http.setTimeout(5000);
    
    int httpCode = http.POST(jsonPayload);
    
    if (httpCode == HTTP_CODE_OK || httpCode == HTTP_CODE_CREATED)
    {
        String response = http.getString();
        logLine("[DeviceRegistration] Success: " + response);
        http.end();
        return true;
    }
    else
    {
        logLine("[DeviceRegistration] Failed: HTTP " + String(httpCode));
        if (httpCode > 0)
        {
            logLine("  Response: " + http.getString());
        }
        http.end();
        return false;
    }
}
