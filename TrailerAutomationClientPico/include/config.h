#pragma once

// Debug logging - set to 0 for production to reduce Serial overhead and save flash/CPU
#define DEBUG_LOGGING 1

// Gateway configuration
#define GATEWAY_DEFAULT_PORT 5000

// mDNS service discovery
#define MDNS_SERVICE_NAME "trailer-gateway"
#define MDNS_SERVICE_PROTO "tcp"
