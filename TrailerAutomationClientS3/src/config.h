#pragma once

// Wi-Fi credentials – update to your network
static const char* WIFI_SSID     = "MM0001";
static const char* WIFI_PASSWORD = "J0shua07";

// These mirror the C# client values in Program.cs
// string clientId = Environment.MachineName;
// string deviceType = "ESP32-S3";
// string friendlyName = "ESP32-S3-SensorNode";
//
// For the S3 firmware, we’ll use S3-specific values.
// You can change these per-device.
static const char* CLIENT_ID      = "ESP32S3-01";
static const char* DEVICE_TYPE    = "ESP32-S3";
static const char* FRIENDLY_NAME  = "S3-SensorNode";

// Gateway details – the gateway advertises itself via mDNS
// with type _trailer-gateway._tcp.local. Port is fixed in
// TrailerAutomationGateway/Program.cs as 5000.
static const uint16_t GATEWAY_DEFAULT_PORT = 5000;

// Heartbeat interval – matches the C# client’s TimeSpan.FromSeconds(10)
static const unsigned long HEARTBEAT_INTERVAL_MS = 10UL * 1000UL;

// mDNS service name parts (C# uses "_trailer-gateway._tcp")
static const char* MDNS_SERVICE_NAME  = "trailer-gateway"; // without leading underscore
static const char* MDNS_SERVICE_PROTO = "tcp";             // without leading underscore
