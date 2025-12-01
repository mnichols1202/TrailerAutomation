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

// Global variable to store last WiFi error
static int g_lastWifiError = 0;

bool ensureWifiConnected()
{
    if (WiFi.status() == WL_CONNECTED)
    {
        return true;
    }

    logLine("Wi-Fi not connected, attempting to connect...");

    // Properly disconnect and reset WiFi to clear any bad state
    // Longer delays for battery-powered operation where power may be less stable
    WiFi.disconnect(true);  // Disconnect and erase stored credentials
    delay(500);
    WiFi.mode(WIFI_OFF);    // Turn off WiFi completely
    delay(500);
    
    // Now start fresh with longer stabilization time
    WiFi.mode(WIFI_STA);
    delay(1000);  // Give radio time to power up properly on battery
    
    // Aggressive WiFi configuration for stability
    WiFi.setAutoReconnect(true);
    WiFi.setSleep(false);  // Disable WiFi sleep for more reliable connection
    WiFi.setTxPower(WIFI_POWER_19_5dBm);  // Max power for better range/stability
    
    // Set DNS servers explicitly (Google DNS as backup)
    WiFi.config(INADDR_NONE, INADDR_NONE, INADDR_NONE, IPAddress(8,8,8,8), IPAddress(8,8,4,4));
    
    WiFi.begin(WIFI_SSID, WIFI_PASSWORD);

    const unsigned long start      = millis();
    const unsigned long timeoutMs  = 20000UL; // 20 seconds for battery-powered scenarios

    while (WiFi.status() != WL_CONNECTED && (millis() - start) < timeoutMs)
    {
        delay(500);
        Serial.print(".");
        
        // Log intermediate status
        if ((millis() - start) % 5000 == 0)
        {
            Serial.print(" [Status: ");
            Serial.print(WiFi.status());
            Serial.print("] ");
        }
    }
    Serial.println();

    if (WiFi.status() == WL_CONNECTED)
    {
        logLine("Wi-Fi connected.");
        Serial.print("  IP address: ");
        Serial.println(WiFi.localIP());
        Serial.print("  Gateway: ");
        Serial.println(WiFi.gatewayIP());
        Serial.print("  DNS: ");
        Serial.println(WiFi.dnsIP());
        Serial.print("  RSSI: ");
        Serial.print(WiFi.RSSI());
        Serial.println(" dBm");
        
        // Test if we can actually reach the gateway
        delay(100);
        
        g_lastWifiError = 0;  // Clear error
        return true;
    }

    // Capture the specific WiFi error status
    g_lastWifiError = WiFi.status();
    
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
