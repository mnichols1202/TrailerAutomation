#include "network.h"

#include <ESPmDNS.h>

#include "config.h"
#include "logging.h"
#include "sdconfig.h"
#include "relaycontrol.h"

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

// Single non-blocking-ish WiFi attempt per call: ~5s worst case.
// TX power is rotated across calls (low → medium → max → low → …) so the
// "try harder over time" behavior is preserved without burning 60+ seconds
// inside one function call. The main loop pages this every iteration; failure
// just means "try again next time" with the next power level.
bool ensureWifiConnected()
{
    if (WiFi.status() == WL_CONNECTED)
    {
        return true;
    }

    const DeviceConfig& config = getDeviceConfig();

    // Rotate TX power across calls. Index persists between calls so each
    // retry uses progressively more power. Reset to 0 (lowest) on success.
    static uint8_t s_powerIdx = 0;
    wifi_power_t powerLevels[3] = {
        WIFI_POWER_11dBm,    // low — minimum interference, lowest current draw
        WIFI_POWER_15dBm,    // medium
        WIFI_POWER_19_5dBm   // max — try harder
    };
    const char* powerNames[3] = { "LOW (11dBm)", "MEDIUM (15dBm)", "MAX (19.5dBm)" };

    logLine("Wi-Fi not connected, attempting (SSID: [" + String(config.wifiSSID) +
            "], TX power: " + String(powerNames[s_powerIdx]) + ")");

    // Make sure the driver is in STA mode. We don't toggle WIFI_OFF every
    // call — that was 2 s of pure delay() per attempt.
    WiFi.mode(WIFI_STA);
    WiFi.setAutoReconnect(true);
    WiFi.setSleep(false);
    WiFi.persistent(false);
    WiFi.setTxPower(powerLevels[s_powerIdx]);

    WiFi.begin(config.wifiSSID, config.wifiPassword);

    const unsigned long timeoutMs = 5000UL;
    const unsigned long start = millis();
    while (WiFi.status() != WL_CONNECTED && (millis() - start) < timeoutMs)
    {
        delay(100);
    }

    if (WiFi.status() == WL_CONNECTED)
    {
        logLine("Wi-Fi connected. IP=" + WiFi.localIP().toString() +
                " RSSI=" + String(WiFi.RSSI()) + " dBm");
        g_lastWifiError = 0;
        s_powerIdx = 0;   // back to lowest power for the next outage
        return true;
    }

    g_lastWifiError = WiFi.status();
    logLine("Wi-Fi attempt failed, status: " + getWiFiStatusString(WiFi.status()));
    s_powerIdx = (s_powerIdx + 1) % 3;   // try a different power next time
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
    // ESPmDNS expects service name and protocol WITHOUT leading underscores.
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

#include "sdconfig.h"
#include <HTTPClient.h>
#include <ArduinoJson.h>

bool registerDevice()
{
    if (!g_gatewayKnown)
    {
        logLine("[DeviceRegistration] Gateway not known, cannot register");
        return false;
    }
    
    if (!isSdConfigLoaded())
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
