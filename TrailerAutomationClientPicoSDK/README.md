# TrailerAutomationClientPicoSDK

Raspberry Pi Pico W client for the Trailer Automation system. This firmware connects to the TrailerAutomationGateway via mDNS discovery and sends periodic heartbeats and sensor readings (temperature and humidity from SHT31 sensor).

## Architecture

This project mirrors the ESP32-S3 client architecture with the following modules:

- **config.h** - WiFi credentials, client ID, device type, and gateway settings
- **logging** - Timestamped serial output for debugging
- **network** - WiFi connection management and mDNS gateway discovery
- **heartbeat** - Periodic heartbeat messages to gateway (`/api/heartbeat`)
- **sensor** - SHT31 temperature/humidity sensor reading and reporting (`/api/sensor-readings`)
- **main.c** - Main loop coordinating WiFi, gateway discovery, heartbeat, and sensor operations

## Hardware Requirements

- Raspberry Pi Pico W
- SHT31 Temperature/Humidity Sensor (I2C)
  - SDA: GPIO 4
  - SCL: GPIO 5
  - VCC: 3.3V
  - GND: GND

## Configuration

Update the following in `src/config.h`:

```c
#define WIFI_SSID     "your-network-name"
#define WIFI_PASSWORD "your-password"
#define CLIENT_ID      "PicoW-01"
#define DEVICE_TYPE    "Raspberry-Pi-Pico-W"
#define FRIENDLY_NAME  "PicoW-SensorNode"
```

## Building

### Prerequisites

1. Install the Pico SDK
2. Set the `PICO_SDK_PATH` environment variable or let CMake fetch it automatically
3. Install CMake (3.13 or later) and ARM GCC compiler

### Build Steps

```powershell
# Create build directory
mkdir build
cd build

# Configure
cmake -G "NMake Makefiles" ..

# Build
nmake
```

The build will produce `TrailerAutomationClient.uf2` in the build directory.

## Flashing

1. Hold the BOOTSEL button on the Pico W while connecting it via USB
2. The Pico will appear as a mass storage device
3. Copy `TrailerAutomationClient.uf2` to the Pico
4. The Pico will automatically reboot and run the firmware

## Operation

1. On startup, the Pico W will:
   - Initialize the SHT31 sensor
   - Connect to WiFi
   - Discover the gateway via mDNS

2. Once connected and gateway is discovered:
   - Sends heartbeat every 10 seconds
   - Sends sensor readings every 30 seconds

3. Serial output (USB CDC) provides detailed logging with timestamps

## Protocol

### Heartbeat (POST /api/heartbeat)
```json
{
  "ClientId": "PicoW-01",
  "DeviceType": "Raspberry-Pi-Pico-W",
  "FriendlyName": "PicoW-SensorNode"
}
```

### Sensor Reading (POST /api/sensor-readings)
```json
{
  "ClientId": "PicoW-01",
  "TemperatureC": 23.45,
  "HumidityPercent": 41.23
}
```

## Troubleshooting

- **WiFi not connecting**: Check SSID and password in config.h
- **Gateway not found**: Ensure gateway is running and advertising via mDNS
- **Sensor errors**: Check I2C wiring (SDA, SCL, VCC, GND) and sensor address (0x44)
- **Serial output**: Connect via USB and use a serial terminal at 115200 baud
