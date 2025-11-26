#include <Arduino.h>
#include <WiFi.h>
#include <HTTPClient.h>
#include <ESPmDNS.h>

#include "config.h"

// Global state for gateway discovery and heartbeat
static String   g_gatewayHost;
static uint16_t g_gatewayPort           = GATEWAY_DEFAULT_PORT;
static bool     g_gatewayKnown          = false;
static unsigned long g_lastHeartbeatMs  = 0;

// Forward declarations
bool ensureWifiConnected();
bool startMdns();
bool discoverGateway();
bool sendHeartbeat();

// Simple helper for logging with a timestamp (millis())
void logLine(const String& msg)
{
    Serial.print("[");
    Serial.print(millis());
    Serial.print(" ms] ");
    Serial.println(msg);
}

void setup()
{
    // Basic serial setup
    Serial.begin(115200);
    // Allow USB-serial to come up
    delay(1000);

    logLine("TrailerAutomationClientS3 starting...");
    logLine("Initializing Wi-Fi...");

    // Connect to Wi-Fi
    if (!ensureWifiConnected())
    {
        logLine("Initial Wi-Fi connection failed, will retry in loop.");
    }

    // Start mDNS so we can do queries and (optionally) be discoverable
    if (!startMdns())
    {
        logLine("mDNS startup failed, gateway discovery may not work.");
    }
    else
    {
        logLine("mDNS initialized, will attempt gateway discovery in loop.");
    }

    // Initial heartbeat timer baseline
    g_lastHeartbeatMs = millis();
}

void loop()
{
    // 1. Make sure we have Wi-Fi
    if (!ensureWifiConnected())
    {
        // If Wi-Fi is not connected, clear gateway state
        if (g_gatewayKnown)
        {
            logLine("Lost Wi-Fi; forgetting previously known gateway.");
            g_gatewayKnown = false;
            g_gatewayHost  = String();
            g_gatewayPort  = GATEWAY_DEFAULT_PORT;
        }

        delay(1000);
        return;
    }

    // 2. Try to discover the gateway if we don't know it yet
    if (!g_gatewayKnown)
    {
        logLine("Gateway not known, attempting mDNS discovery...");
        if (!discoverGateway())
        {
            logLine("Gateway discovery attempt failed; will retry.");
            delay(2000);
            return;
        }

        logLine("Gateway discovered, will start heartbeat loop.");
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

    // 4. Short delay so we don't spin aggressively
    delay(10);
}

// ----------------------------------------------------------------------------
// Wi-Fi helpers
// ----------------------------------------------------------------------------

bool ensureWifiConnected()
{
    if (WiFi.status() == WL_CONNECTED)
    {
        return true;
    }

    logLine("Wi-Fi not connected, attempting to connect...");

    // Start connection
    WiFi.mode(WIFI_STA);
    WiFi.begin(WIFI_SSID, WIFI_PASSWORD);

    const unsigned long start = millis();
    const unsigned long timeoutMs = 15000UL; // 15 seconds

    while (WiFi.status() != WL_CONNECTED && (millis() - start) < timeoutMs)
    {
        delay(500);
        Serial.print(".");
    }
    Serial.println();

    if (WiFi.status() == WL_CONNECTED)
    {
        logLine("Wi-Fi connected.");
        Serial.print("  IP address: ");
        Serial.println(WiFi.localIP());
        return true;
    }

    logLine("Wi-Fi connection attempt timed out.");
    return false;
}

// ----------------------------------------------------------------------------
// mDNS helpers
// ----------------------------------------------------------------------------

bool startMdns()
{
    // Use CLIENT_ID as the mDNS hostname so you can see it on the LAN if desired
    if (!MDNS.begin(CLIENT_ID))
    {
        logLine("MDNS.begin() failed.");
        return false;
    }

    logLine("mDNS responder started.");
    // Optionally, you could advertise the client itself as some service here.
    return true;
}

bool discoverGateway()
{
    // Query for the same service type used by TrailerAutomationGateway:
    // C# uses "_trailer-gateway._tcp" (see MdnsHost.cs).
    // ESPmDNS expects service name and protocol WITHOUT leading underscores.
    int n = MDNS.queryService(MDNS_SERVICE_NAME, MDNS_SERVICE_PROTO);

    if (n <= 0)
    {
        logLine("mDNS queryService returned no results.");
        return false;
    }

    // For now, just take the first result
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

// ----------------------------------------------------------------------------
// Heartbeat logic – mirrors TrailerAutomationClientNet/Program.cs
// ----------------------------------------------------------------------------

bool sendHeartbeat()
{
    if (!g_gatewayKnown)
    {
        logLine("sendHeartbeat() called but gateway is unknown.");
        return false;
    }

    if (WiFi.status() != WL_CONNECTED)
    {
        logLine("sendHeartbeat() called but Wi-Fi is not connected.");
        return false;
    }

    // Build URL: http://<gatewayHost>:<port>/api/heartbeat
    String url = String("http://") + g_gatewayHost + ":" + String(g_gatewayPort) + "/api/heartbeat";

    // Build JSON payload mirroring the anonymous object in Program.cs:
    // var hb = new { ClientId = clientId, DeviceType = deviceType, FriendlyName = friendlyName };
    String payload = String("{\"ClientId\":\"") + CLIENT_ID +
                     "\",\"DeviceType\":\"" + DEVICE_TYPE +
                     "\",\"FriendlyName\":\"" + FRIENDLY_NAME + "\"}";

    logLine(String("Sending heartbeat to ") + url);
    logLine(String("Payload: ") + payload);

    HTTPClient http;
    http.begin(url);
    http.addHeader("Content-Type", "application/json");

    int httpCode = http.POST((uint8_t*)payload.c_str(), payload.length());

    if (httpCode <= 0)
    {
        logLine(String("HTTP POST failed: ") + http.errorToString(httpCode));
        http.end();
        return false;
    }

    String response = http.getString();
    http.end();

    Serial.print("Heartbeat response code: ");
    Serial.println(httpCode);
    Serial.print("Heartbeat response body: ");
    Serial.println(response);

    // Mirror the C# behavior of EnsureSuccessStatusCode()
    if (httpCode < 200 || httpCode >= 300)
    {
        logLine("Heartbeat received non-success HTTP status.");
        return false;
    }

    return true;
}
