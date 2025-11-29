# TrailerAutomationClientPico

MicroPython 1.26 implementation for Raspberry Pi Pico 2 W - A port of the TrailerAutomationClientNet project.

## Overview

This project provides a MicroPython-based client for the Trailer Automation system, designed to run on a Raspberry Pi Pico 2 W. It reads temperature and humidity data from an SHT31 sensor and communicates with the TrailerAutomationGateway via HTTP.

## Features

- **WiFi Connectivity**: Automatic connection to configured WiFi network
- **mDNS Service Discovery**: Automatically discovers the TrailerAutomationGateway on the local network
- **SHT31 Sensor Support**: Reads temperature and humidity via I2C
- **Dual Loops**: 
  - Heartbeat loop (10 seconds) - keeps the gateway informed of device presence
  - Sensor reading loop (30 seconds) - sends temperature/humidity data
- **LED Status Indicators**: Onboard LED provides visual feedback
- **Multi-threading**: Sensor readings run in a separate thread from heartbeat

## Hardware Requirements

- **Raspberry Pi Pico 2 W** (or Pico W with MicroPython 1.26+)
- **SHT31 Temperature/Humidity Sensor**
- **I2C Connection**:
  - SDA → GP0 (default, configurable)
  - SCL → GP1 (default, configurable)
  - VCC → 3.3V
  - GND → GND

## Wiring Diagram

```
Pico 2 W          SHT31
--------          -----
3.3V (Pin 36) --> VCC
GND  (Pin 38) --> GND
GP0  (Pin 1)  --> SDA
GP1  (Pin 2)  --> SCL
```

## Software Requirements

- **MicroPython 1.26** or later
- Libraries (included in standard MicroPython):
  - `urequests` (for HTTP requests)
  - `network` (for WiFi)
  - `machine` (for I2C and GPIO)

## Installation

### 1. Flash MicroPython

Download and flash MicroPython 1.26+ to your Pico 2 W:
```
https://micropython.org/download/rp2-pico-w/
```

### 2. Configure Settings

Edit `config.py` and update the following:

```python
# WiFi Settings
WIFI_SSID = "YourWiFiSSID"
WIFI_PASSWORD = "YourWiFiPassword"

# Device Identity
FRIENDLY_NAME = "Pico2W-SensorNode"

# I2C Pins (if different from default)
I2C_SDA_PIN = 0  # GP0
I2C_SCL_PIN = 1  # GP1
```

### 3. Upload Files

Upload all Python files to the Pico 2 W:
- `main.py`
- `config.py`
- `sht31_reader.py`
- `gateway_discovery.py`

You can use tools like:
- **Thonny IDE** (easiest for beginners)
- **rshell**
- **mpremote**
- **ampy**

### 4. Upload via Thonny (Recommended)

1. Open Thonny IDE
2. Connect your Pico 2 W via USB
3. Select **MicroPython (Raspberry Pi Pico)** interpreter
4. Open each `.py` file
5. Save to "Raspberry Pi Pico" (not your computer)
6. The device will auto-run `main.py` on boot

### 5. Upload via mpremote

```powershell
# Connect to Pico
mpremote connect COM3

# Upload files
mpremote fs cp main.py :main.py
mpremote fs cp config.py :config.py
mpremote fs cp sht31_reader.py :sht31_reader.py
mpremote fs cp gateway_discovery.py :gateway_discovery.py

# Reset to run
mpremote reset
```

## Configuration

All configuration is done in `config.py`:

| Setting | Default | Description |
|---------|---------|-------------|
| `WIFI_SSID` | - | Your WiFi network name |
| `WIFI_PASSWORD` | - | Your WiFi password |
| `CLIENT_ID` | `None` | Device ID (auto-generated if None) |
| `DEVICE_TYPE` | `"Pico2W"` | Device type identifier |
| `FRIENDLY_NAME` | `"Pico2W-SensorNode"` | Human-readable name |
| `HEARTBEAT_INTERVAL_SEC` | `10` | Heartbeat interval |
| `SENSOR_REPORT_INTERVAL_SEC` | `30` | Sensor reading interval |
| `MDNS_DISCOVERY_TIMEOUT_SEC` | `8` | mDNS discovery timeout |
| `I2C_BUS` | `0` | I2C bus number |
| `I2C_SDA_PIN` | `0` | GPIO pin for SDA (GP0) |
| `I2C_SCL_PIN` | `1` | GPIO pin for SCL (GP1) |
| `SHT31_ADDRESS` | `0x44` | I2C address (0x44 or 0x45) |

## Usage

### Automatic Start

Once uploaded, the Pico will automatically run `main.py` on boot. The program will:

1. Connect to WiFi
2. Discover the gateway via mDNS
3. Start sending heartbeats every 10 seconds
4. Start reading and sending sensor data every 30 seconds

### Manual Start

Connect via serial and run:
```python
import main
```

### Monitor Output

Connect via serial (115200 baud) to see status messages:

```
==================================================
TrailerAutomationClientPico starting...
MicroPython version: e66118682c374338
==================================================
Connecting to WiFi: MyNetwork
Connected! IP: 192.168.1.50
[mDNS] Discovering _trailer-gateway._tcp.local...
[mDNS] Found SRV: target=gateway.local, port=5000
[mDNS] Found A: gateway.local -> 192.168.1.100
Gateway discovered: http://192.168.1.100:5000
ClientId: e66118682c374338
DeviceType: Pico2W
FriendlyName: Pico2W-SensorNode
Starting sensor loop...
SHT31 found at address 0x44
Starting heartbeat loop...
[...] Local sensor reading: TempC=22.45 Humidity=45.67
[...] Heartbeat OK: {"status":"ok"}
```

## LED Status Indicators

| Pattern | Meaning |
|---------|---------|
| Single blink (50ms) | WiFi connection attempt |
| 3 blinks (100ms each) | WiFi connected successfully |
| Single blink (50ms) | Heartbeat sent successfully |

## Troubleshooting

### WiFi Won't Connect
- Verify `WIFI_SSID` and `WIFI_PASSWORD` in `config.py`
- Ensure the Pico is within WiFi range
- Check that your WiFi is 2.4GHz (Pico W doesn't support 5GHz)

### SHT31 Not Found
- Check I2C wiring (SDA, SCL, VCC, GND)
- Verify I2C address (run I2C scan: `i2c.scan()`)
- Try address `0x45` if `0x44` doesn't work
- Check power supply (3.3V)

### Gateway Not Discovered
- Ensure TrailerAutomationGateway is running
- Verify both devices are on the same network
- Check gateway is advertising mDNS service
- Try increasing `MDNS_DISCOVERY_TIMEOUT_SEC`

### Memory Errors
- MicroPython has limited RAM
- Reduce buffer sizes if needed
- Increase sensor/heartbeat intervals

## API Endpoints

The client communicates with these gateway endpoints:

### POST /api/heartbeat
```json
{
  "ClientId": "e66118682c374338",
  "DeviceType": "Pico2W",
  "FriendlyName": "Pico2W-SensorNode"
}
```

### POST /api/sensor-readings
```json
{
  "ClientId": "e66118682c374338",
  "TemperatureC": 22.45,
  "HumidityPercent": 45.67
}
```

## File Structure

```
TrailerAutomationClientPico/
├── main.py                  # Main program entry point
├── config.py                # Configuration settings
├── sht31_reader.py          # SHT31 sensor driver
├── gateway_discovery.py     # mDNS service discovery
└── README.md               # This file
```

## Differences from .NET Version

| Feature | .NET Version | MicroPython Version |
|---------|--------------|---------------------|
| Platform | Raspberry Pi + .NET | Pico 2 W + MicroPython |
| Threading | `Task.Run()` | `_thread.start_new_thread()` |
| HTTP Client | `HttpClient` | `urequests` |
| mDNS Library | `Makaretu.Dns` | Custom implementation |
| I2C Library | `Iot.Device.Bindings` | Custom SHT31 driver |
| Machine Name | `Environment.MachineName` | `machine.unique_id()` |

## Performance Notes

- **Memory**: MicroPython has ~200KB of free RAM
- **Threading**: Limited to 2 threads (main + sensor)
- **Network**: HTTP requests are blocking
- **Timing**: Less precise than .NET (±100ms)

## License

This project is part of the TrailerAutomation system.

## Related Projects

- **TrailerAutomationClientNet**: Original .NET implementation
- **TrailerAutomationGateway**: Central gateway service
- **TrailerAutomationClientS3**: ESP32-S3 implementation

## Support

For issues or questions, please check the main TrailerAutomation repository.
