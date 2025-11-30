#include "network.h"
#include "config.h"
#include "logging.h"
#include "pico/stdlib.h"
#include "pico/cyw43_arch.h"
#include "lwip/dns.h"
#include "lwip/pbuf.h"
#include "lwip/udp.h"
#include <string.h>
#include <stdio.h>

// Internal gateway state
static char g_gateway_host[128] = {0};
static uint16_t g_gateway_port = GATEWAY_DEFAULT_PORT;
static bool g_gateway_known = false;

// -----------------------------------------------------------------------------
// Wi-Fi
// -----------------------------------------------------------------------------

bool ensure_wifi_connected(void) {
    if (cyw43_wifi_link_status(&cyw43_state, CYW43_ITF_STA) == CYW43_LINK_UP) {
        return true;
    }

    log_line("Wi-Fi not connected, attempting to connect...");

    cyw43_arch_enable_sta_mode();

    int result = cyw43_arch_wifi_connect_timeout_ms(
        WIFI_SSID,
        WIFI_PASSWORD,
        CYW43_AUTH_WPA2_AES_PSK,
        15000  // 15 second timeout
    );

    if (result == 0) {
        log_line("Wi-Fi connected.");
        
        // Print IP address
        char ip_str[32];
        const ip4_addr_t *addr = netif_ip4_addr(netif_default);
        snprintf(ip_str, sizeof(ip_str), "  IP address: %s", ip4addr_ntoa(addr));
        log_line(ip_str);
        
        return true;
    }

    log_line("Wi-Fi connection attempt timed out.");
    return false;
}

// -----------------------------------------------------------------------------
// mDNS Gateway Discovery
// -----------------------------------------------------------------------------

// Simple mDNS query implementation for discovering the gateway
// This is a simplified version - full mDNS is complex
// For Pico W, we'll use a DNS-SD query approach

static void mdns_result_callback(const char *name, const ip_addr_t *ipaddr, void *callback_arg) {
    if (ipaddr != NULL) {
        // Successfully resolved the gateway
        snprintf(g_gateway_host, sizeof(g_gateway_host), "%s", ip4addr_ntoa(ipaddr));
        g_gateway_port = GATEWAY_DEFAULT_PORT;
        g_gateway_known = true;
        
        char msg[160];
        snprintf(msg, sizeof(msg), "Gateway discovered at http://%s:%u", 
                 g_gateway_host, g_gateway_port);
        log_line(msg);
    } else {
        log_line("mDNS query returned no results.");
    }
}

bool discover_gateway(void) {
    // For Pico W, we'll use a simplified mDNS query approach
    // Query for the service name directly
    // Note: Full mDNS/DNS-SD support on Pico W requires additional libraries
    
    log_line("Attempting mDNS gateway discovery...");
    
    // Try to resolve the mDNS hostname
    ip_addr_t gateway_addr;
    cyw43_arch_lwip_begin();
    err_t err = dns_gethostbyname(MDNS_SERVICE_NAME, &gateway_addr, mdns_result_callback, NULL);
    cyw43_arch_lwip_end();
    
    if (err == ERR_OK) {
        // Immediate resolution
        snprintf(g_gateway_host, sizeof(g_gateway_host), "%s", ip4addr_ntoa(&gateway_addr));
        g_gateway_port = GATEWAY_DEFAULT_PORT;
        g_gateway_known = true;
        
        char msg[160];
        snprintf(msg, sizeof(msg), "Gateway discovered at http://%s:%u", 
                 g_gateway_host, g_gateway_port);
        log_line(msg);
        return true;
    } else if (err == ERR_INPROGRESS) {
        // DNS lookup in progress, wait a bit
        log_line("DNS lookup in progress...");
        sleep_ms(2000);
        
        if (g_gateway_known) {
            return true;
        }
    }
    
    log_line("Gateway discovery failed. Ensure gateway is advertising via mDNS.");
    return false;
}

// -----------------------------------------------------------------------------
// Gateway state accessors
// -----------------------------------------------------------------------------

bool is_gateway_known(void) {
    return g_gateway_known;
}

void forget_gateway(void) {
    g_gateway_known = false;
    memset(g_gateway_host, 0, sizeof(g_gateway_host));
    g_gateway_port = GATEWAY_DEFAULT_PORT;
}

const char* get_gateway_host(void) {
    return g_gateway_host;
}

uint16_t get_gateway_port(void) {
    return g_gateway_port;
}
