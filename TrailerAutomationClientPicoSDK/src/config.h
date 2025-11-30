#ifndef CONFIG_H
#define CONFIG_H

// Wi-Fi credentials – update to your network
#define WIFI_SSID     "MM0001"
#define WIFI_PASSWORD "J0shua07"

// Client identification
// For the Pico W firmware, use Pico-specific values.
// You can change these per-device.
#define CLIENT_ID      "PicoW-01"
#define DEVICE_TYPE    "Raspberry-Pi-Pico-W"
#define FRIENDLY_NAME  "PicoW-SensorNode"

// Gateway details – the gateway advertises itself via mDNS
// with type _trailer-gateway._tcp.local. Port is fixed in
// TrailerAutomationGateway/Program.cs as 5000.
#define GATEWAY_DEFAULT_PORT 5000

// Heartbeat interval – matches the C# client's TimeSpan.FromSeconds(10)
#define HEARTBEAT_INTERVAL_MS (10UL * 1000UL)

// Sensor report interval (milliseconds)
#define SENSOR_INTERVAL_MS (30UL * 1000UL)  // 30 seconds

// mDNS service name parts (C# uses "_trailer-gateway._tcp")
#define MDNS_SERVICE_NAME  "_trailer-gateway._tcp.local"

#endif // CONFIG_H
