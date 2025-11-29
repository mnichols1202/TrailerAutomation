#include "network.h"

#include <ESPmDNS.h>

#include "config.h"
#include "logging.h"

// Internal gateway state
static String   g_gatewayHost;
static uint16_t g_gatewayPort  = GATEWAY_DEFAULT_PORT;
static bool     g_gatewayKnown = false;

// -----------------------------------------------------------------------------
// Wi-Fi
// -----------------------------------------------------------------------------

bool ensureWifiConnected()
{
    if (WiFi.status() == WL_CONNECTED)
    {
        return true;
    }

    logLine("Wi-Fi not connected, attempting to connect...");

    WiFi.mode(WIFI_STA);
    WiFi.begin(WIFI_SSID, WIFI_PASSWORD);

    const unsigned long start      = millis();
    const unsigned long timeoutMs  = 15000UL; // 15 seconds

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

// -----------------------------------------------------------------------------
// mDNS
// -----------------------------------------------------------------------------

bool startMdns()
{
    // Use CLIENT_ID as the mDNS hostname so you can see it on the LAN if desired
    if (!MDNS.begin(CLIENT_ID))
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
