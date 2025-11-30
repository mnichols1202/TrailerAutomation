#ifndef NETWORK_H
#define NETWORK_H

#include <stdbool.h>
#include <stdint.h>

// Ensure Wi-Fi is connected (tries to reconnect if needed)
bool ensure_wifi_connected(void);

// Discover the TrailerAutomationGateway via mDNS
bool discover_gateway(void);

// Gateway state accessors
bool is_gateway_known(void);
void forget_gateway(void);
const char* get_gateway_host(void);
uint16_t get_gateway_port(void);

#endif // NETWORK_H
