#include "network.h"

#include <LEAmDNS.h>

#include "config.h"
#include "logging.h"
#include "fsconfig.h"
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

// Single non-blocking-ish WiFi attempt: total worst case ~5.5s.
// Returns true if already (or now) connected, false otherwise. The main
// loop pages this on each iteration, so failure just means "try again next
// time" — no inner retry loop is needed.
bool ensureWifiConnected()
{
    if (WiFi.status() == WL_CONNECTED)
    {
        return true;
    }

    const DeviceConfig& config = getDeviceConfig();

    // First call after a failure (or first call period): kick off a fresh
    // connect attempt. We do NOT cycle WIFI_OFF → WIFI_STA every call — that
    // was 2 s of pure delay() on every iteration. WiFi.begin() called against
    // an already-STA-mode driver just restarts the association.
    logLine("Wi-Fi not connected, starting attempt (SSID: [" + String(config.wifiSSID) + "])");

    WiFi.mode(WIFI_STA);
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
        return true;
    }

    g_lastWifiError = WiFi.status();
    logLine("Wi-Fi attempt failed, status: " + getWiFiStatusString(WiFi.status()));
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

// Single mDNS query per call (~1.3s worst case). The main loop paces retries
// with its own backoff delay so this function doesn't need an inner retry
// loop. The MDNS.end()+begin() reset stays — LEAmDNS leaves a broken receive
// socket after a failed queryService(), so we tear it down on every attempt.
// (ESPmDNS on the S3 does not have this bug; same pattern is unnecessary there.)
bool discoverGateway()
{
    const DeviceConfig& config = getDeviceConfig();

    MDNS.end();
    delay(100);
    if (!MDNS.begin(config.clientId))
    {
        logLine("mDNS restart failed");
    }
    delay(200);

    int n = MDNS.queryService(MDNS_SERVICE_NAME, MDNS_SERVICE_PROTO);

    if (n > 0)
    {
        IPAddress hostIp = MDNS.IP(0);
        uint16_t  port   = MDNS.port(0);

        if (hostIp)
        {
            g_gatewayHost  = hostIp.toString();
            g_gatewayPort  = (port != 0) ? port : GATEWAY_DEFAULT_PORT;
            g_gatewayKnown = true;
            logLine("Gateway discovered at http://" + g_gatewayHost + ":" + String(g_gatewayPort));
            return true;
        }
        logLine("mDNS result had no valid IP");
    }
    else
    {
        logLine("mDNS query returned no results; gateway may still be initializing");
    }

    return false;
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
