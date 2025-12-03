#pragma once

#include <cstdint>

// NOTE: WiFi credentials, ClientId, DeviceType, FriendlyName, and intervals
// are now loaded from config.json on LittleFS filesystem.
// Use 'pio run --target uploadfs' to upload the data/config.json file.

// Gateway details – the gateway advertises itself via mDNS
// with type _trailer-gateway._tcp.local. Port is fixed in
// TrailerAutomationGateway/Program.cs as 5000.
static const uint16_t GATEWAY_DEFAULT_PORT = 5000;

// mDNS service name parts (C# uses "_trailer-gateway._tcp")
static const char* MDNS_SERVICE_NAME  = "trailer-gateway"; // without leading underscore
static const char* MDNS_SERVICE_PROTO = "tcp";             // without leading underscore
