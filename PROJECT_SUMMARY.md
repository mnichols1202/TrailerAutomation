# Trailer Automation System - Project Summary

**Last Updated:** December 7, 2025  
**Status:** All 4 projects stable and tested

---

## System Architecture

### Overview
Hub-and-spoke architecture with centralized gateway and multiple client devices for RV trailer automation.

**Components:**
1. **TrailerAutomationGateway** - ASP.NET Core web server (Raspberry Pi)
2. **TrailerAutomationClientNet** - .NET 9 GPIO client (Raspberry Pi)
3. **TrailerAutomationClientS3** - ESP32-S3 client (Arduino/C++)
4. **TrailerAutomationClientPico** - Pico W/2W client (Arduino/C++)

---

## Gateway (TrailerAutomationGateway)

### Platform
- **Framework:** ASP.NET Core 9.0 (Blazor Server)
- **Target:** Raspberry Pi (ARM64 Linux)
- **Port:** 5000 (HTTP)

### Features
- **Device Registry:** Tracks all connected clients with heartbeat monitoring (60s interval, 3 missed = timeout)
- **Web UI:** Real-time device control and monitoring via Blazor/SignalR
- **Relay Control:** Toggle relays on any registered device
- **Device Management:** Reboot, re-initialize, view configuration
- **Sensor Data:** Collects and stores temperature/humidity readings (LiteDB)
- **History:** Sensor reading history with charts
- **mDNS Discovery:** Advertises as `_trailer-gateway._tcp.local`

### Key Files
- `Program.cs` - API endpoints, mDNS, services
- `ClientRegistry.cs` - Device tracking and state management
- `DeviceCommandService.cs` - TCP command sender
- `Components/Pages/Devices.razor` - Device control UI with info modal

### Database
- **LiteDB** - Stores sensor readings in `data/sensor-readings.db`

---

## .NET Client (TrailerAutomationClientNet)

### Platform
- **Framework:** .NET 9.0
- **Target:** Raspberry Pi (ARM64 Linux)
- **GPIO:** System.Device.Gpio
- **I2C Sensors:** Iot.Device.Bindings (SHT31)

### Features
- **GPIO Relay Control:** Up to 4 relays via GPIO pins
- **Physical Buttons:** Local and remote relay control with debounce
- **SHT31 Sensors:** Temperature and humidity monitoring
- **Gateway Discovery:** mDNS client
- **TCP Command Listener:** Port 8888 for gateway commands
- **Heartbeat:** 60s interval to gateway
- **Auto-Registration:** Sends device info and capabilities on startup

### Key Files
- `Program.cs` - Main loop, heartbeat, sensor readings
- `CommandListener.cs` - TCP server for gateway commands
- `ButtonController.cs` - Physical button monitoring with debounce
- `GpioRelayController.cs` - Relay state management
- `Sht31Reader.cs` - I2C sensor interface
- `config.json` - Device configuration

### Configuration Example
```json
{
  "Device": {
    "ClientId": "Pi-3B-001",
    "DeviceType": "RPi",
    "FriendlyName": "Bedroom Sensor Node",
    "CommandListenerPort": 8888
  },
  "Intervals": {
    "HeartbeatSeconds": 60
  },
  "Hardware": {
    "Relays": [...],
    "Sensors": [...],
    "Buttons": [...]
  }
}
```

---

## ESP32-S3 Client (TrailerAutomationClientS3)

### Platform
- **MCU:** ESP32-S3 N16R8 (16MB Flash, 8MB PSRAM OPI)
- **Framework:** Arduino (PlatformIO)
- **Filesystem:** LittleFS
- **Config:** Embedded in firmware via `data/config.json`

### Features
- **WiFi Connectivity:** 2.4GHz 802.11 b/g/n
- **GPIO Relay Control:** Up to 4 relays (USB power limit)
- **Physical Buttons:** Local and remote relay control with debounce
- **RGB LED Status:** Boot/WiFi/gateway connection indicators
- **Power Monitoring:** Battery operation support
- **SHT31 Sensors:** I2C temperature/humidity (optional)
- **PSRAM Enabled:** WiFi stack and JSON parsing use PSRAM

### Key Files
- `src/main.cpp` - Main loop, WiFi, heartbeat, sensors
- `src/commandlistener.cpp` - TCP command handler
- `src/button.cpp` - Button debounce with rising edge detection
- `src/relaycontrol.cpp` - GPIO relay management
- `src/sdconfig.cpp` - LittleFS config loader
- `src/network.cpp` - WiFi and mDNS
- `src/rgbled.cpp` - Status LED animations
- `platformio.ini` - Build configuration

### Memory Configuration
```ini
board_build.arduino.memory_type = qio_opi
board_build.psram_type = opi
-DCONFIG_SPIRAM_USE_MALLOC=1  # PSRAM for WiFi/JSON
```

### Power Budget
- **USB Power:** 4 relays max @ 80mA each = ~320mA
- **External 5V:** Up to 20+ relays with separate power supply

---

## Pico W/2W Client (TrailerAutomationClientPico)

### Platform
- **MCU:** RP2040 (Pico W) or RP2350 (Pico 2W)
- **Framework:** Arduino (PlatformIO)
- **Filesystem:** LittleFS
- **Config:** Embedded in firmware via `data/config.json`

### Features
- Identical functionality to ESP32-S3 client
- Supports both Pico W and Pico 2W with separate build environments
- Same 4-relay USB power limit

### Build Environments
```bash
# Pico W (RP2040)
pio run -e picow -t upload
pio run -e picow -t uploadfs

# Pico 2W (RP2350)
pio run -e pico2w -t upload
pio run -e pico2w -t uploadfs
```

### Key Files
- Same structure as ESP32-S3 client
- `src/fsconfig.cpp` - LittleFS config loader (Pico-specific)

---

## Communication Protocol

### Client → Gateway

**1. Heartbeat (HTTP POST `/api/heartbeat`)**
```json
{
  "ClientId": "device-id",
  "DeviceType": "RPi|ESP32-S3|Pico2W",
  "FriendlyName": "Device Name"
}
```
Response includes `needsRegistration` flag if gateway restarted.

**2. Device Registration (HTTP POST `/api/register`)**
```json
{
  "ClientId": "device-id",
  "DeviceType": "RPi",
  "FriendlyName": "Device Name",
  "IpAddress": "192.168.1.100",
  "CommandPort": 8888,
  "Capabilities": ["relay", "sensor"],
  "Relays": [
    {"Id": "relay1", "Name": "LED"}
  ]
}
```

**3. Sensor Readings (HTTP POST `/api/sensor-readings`)**
```json
{
  "ClientId": "device-id",
  "TemperatureC": 23.45,
  "HumidityPercent": 41.23
}
```

**4. Relay State Notification (HTTP POST `/api/devices/{id}/relays/{relayId}/state`)**
```json
{
  "state": "on"
}
```
Used when local button changes relay state.

### Gateway → Client (TCP Commands)

**Command Format:** Line-delimited JSON on port 8888

**Commands:**
- `ping` - Connectivity check
- `identify` - Get device identity
- `init` - Trigger re-registration
- `reboot` - Restart device
- `setRelay` - Set relay state
- `getRelayState` - Query relay state
- `getConfig` - Retrieve full device configuration

**Example Command:**
```json
{
  "commandId": "uuid",
  "type": "setRelay",
  "payload": {
    "relayId": "relay1",
    "state": "on"
  }
}
```

**Response:**
```json
{
  "commandId": "uuid",
  "success": true,
  "message": "Relay 'relay1' set to on",
  "data": {
    "relayId": "relay1",
    "state": "on"
  }
}
```

---

## Button Logic

### Hardware Configuration
- **Active LOW** with `INPUT_PULLUP`
- Button pressed = GPIO reads LOW
- Button released = GPIO reads HIGH

### Debounce Implementation
```
lastValue - immediate reading (updated every loop)
lastStableValue - reading after 50ms debounce period

Detection: Rising edge (LOW → HIGH) after debounce
Trigger: Button RELEASE (not press)
```

### Button Types
1. **Local Button:** TargetDevice = own ClientId → Direct GPIO control
2. **Remote Button:** TargetDevice = other ClientId → HTTP to gateway `/toggle` endpoint

---

## Development Patterns

### Source of Truth
**MANDATE:** .NET projects are the backbone and source of truth.
- All new features implemented in .NET first
- Embedded clients (S3/Pico) updated only when explicitly instructed
- Never make speculative changes to embedded clients

### Feature Parity
All three clients (NET, S3, Pico) must have functionally identical behavior:
- Same command handlers
- Same button logic
- Same relay control
- Same sensor handling

### Configuration
- JSON-based for all platforms
- Same structure across NET/S3/Pico
- Per-sensor reading intervals
- Per-button target configuration

---

## Hardware Specifications

### ESP32-S3 Relay Limits
- **USB Powered:** 4 relays max (400mA available)
- **External 5V:** 20+ relays (2A+ supply)
- **Relay Current:** ~80mA @ 5V per AITRIP 3V module
- **GPIO Current:** ~5-10mA per optocoupler LED

### Pico W/2W Relay Limits
- **USB Powered:** 4 relays max (400-450mA available)
- **External 5V:** Similar to ESP32-S3
- **Continuous Operation:** No thermal concerns for days/weeks

### Power Supplies
- **Recommended:** Anker 735 GaNPrime 65W USB-C charger
- **Per Port:** 5V @ 3A (15W)
- **Usage:** ~500mA per board with 4 relays = 16% of capacity

---

## Web UI Features

### Devices Page
- **Device List:** All registered clients with online/offline status
- **Relay Control:** Toggle buttons for each relay (color-coded on/off)
- **Actions:**
  - ℹ️ **Info Button** - View device configuration in modal
  - 🔄 **Reboot** - Restart individual device
  - 🔄 **Re-Init All** - Trigger re-registration of all devices
- **Auto-Refresh:** 30s background updates (relay changes via SignalR)

### Sensors Page
- **Current Readings:** Latest temperature/humidity from all sensors
- **Sensor Info:** ClientId, device name, last reading timestamp

### History Page
- **Charts:** Temperature and humidity over time (Chart.js)
- **Time Ranges:** Last hour, 6 hours, 24 hours, 7 days

---

## Service Configuration

### Gateway Service (systemd)
```bash
sudo nano /etc/systemd/system/trailer-automation-gateway.service
sudo systemctl enable trailer-automation-gateway
sudo systemctl start trailer-automation-gateway
```

### Client Service (systemd)
```bash
sudo nano /etc/systemd/system/trailer-automation-client.service
sudo systemctl enable trailer-automation-client
sudo systemctl start trailer-automation-client
```

---

## Build & Deploy

### Gateway
```bash
cd TrailerAutomationGateway
dotnet publish -c Release -r linux-arm64 --self-contained
# Copy to Pi: /home/pi/TrailerAutomationGateway/
```

### .NET Client
```bash
cd TrailerAutomationClientNet
dotnet publish -c Release -r linux-arm64 --self-contained
# Copy to Pi: /home/pi/TrailerAutomationNet/
```

### ESP32-S3
```bash
cd TrailerAutomationClientS3
pio run -e trailer_automation_client_s3 -t upload
pio run -e trailer_automation_client_s3 -t uploadfs
# Or: pio run -e trailer_automation_client_s3 -t buildunified
```

### Pico W/2W
```bash
cd TrailerAutomationClientPico
# Pico W:
pio run -e picow -t buildunified  # FW+FS in one .uf2
# Pico 2W:
pio run -e pico2w -t buildunified
# Drag .pio/build/{env}/firmware-unified.uf2 to RPI-RP2 drive
```

---

## Known Issues & Solutions

### ESP32-S3 PSRAM
- **Enabled:** WiFi stack and JSON use PSRAM automatically
- **Benefits:** Improved WiFi stability, reduced DRAM pressure

### Pico W/2W Upload
- **Method:** Use `upload_protocol = mbed` for reliability
- **BOOTSEL Mode:** Hold button while plugging in USB
- **Unified Build:** Combines firmware + filesystem in one .uf2

### Button Edge Detection
- **Current:** Rising edge (release) after 50ms debounce
- **Rationale:** Better debounce reliability, prevents accidental triggers
- **Alternative:** Can switch to falling edge (press) if preferred

### Gateway Browser Refresh
- **Issue:** ASP.NET Core dev middleware logs were noisy
- **Fix:** Added logging filter to suppress BrowserRefresh debug output
- **Note:** Does not affect Blazor SignalR UI updates

---

## Performance Metrics

### Memory Usage
- **Gateway:** ~100-150MB RAM (Blazor + LiteDB)
- **.NET Client:** ~50-80MB RAM
- **ESP32-S3:** ~200KB DRAM used, PSRAM for WiFi/heap
- **Pico W:** ~100KB RAM used

### Network Traffic
- **Heartbeat:** Every 60s per client (~200 bytes)
- **Sensor Readings:** Per-sensor interval (300-600s typical, ~150 bytes)
- **Button Press:** Immediate (~200 bytes)
- **Command:** On-demand via TCP (~200-500 bytes)

### Response Times
- **Button Press → Relay:** <100ms local, <300ms remote
- **Web UI Toggle:** <500ms including SignalR update
- **Gateway Discovery:** <2s on boot

---

## Testing Status

✅ **Gateway** - Stable, all features tested  
✅ **.NET Client** - Stable, GPIO/sensors working  
✅ **ESP32-S3 Client** - Stable, deployed and tested  
✅ **Pico W/2W Client** - Stable, both variants tested

### Test Coverage
- Device registration and heartbeat
- Relay control (local and remote)
- Physical button operation (both local and remote targets)
- Sensor readings (SHT31 on .NET client)
- Gateway web UI (all pages functional)
- Device configuration retrieval
- Re-initialization and reboot commands
- Multi-device coordination

---

## Future Enhancements (Planned)

_To be discussed - ready for more complex functionality_

---

## Development Environment

### Tools
- **IDE:** Visual Studio Code
- **Embedded:** PlatformIO
- **.NET:** .NET 9 SDK
- **Version Control:** Git (GitHub: mnichols1202/TrailerAutomation)

### Platforms
- **Development:** Windows 11 (PowerShell)
- **Production:** Raspberry Pi OS Bookworm (ARM64)
- **Embedded:** ESP32-S3, Pico W, Pico 2W

---

## Quick Reference

### Port Numbers
- Gateway HTTP: **5000**
- Client TCP: **8888**
- mDNS Service: **_trailer-gateway._tcp.local**

### Timing
- Heartbeat: **60s**
- Heartbeat Timeout: **180s** (3 missed)
- Button Debounce: **50ms**
- Sensor Intervals: **300-600s** (configurable per-sensor)

### GPIO Pins (Example Config)
- **Relays:** 4, 6-9 (Pico), 17, 22, 27 (.NET)
- **Buttons:** 0-3, 12, 15
- **I2C (SHT31):** Default SDA/SCL (Pico: 4/5, Pi: 2/3)

### Build Artifacts
- Gateway: `bin/Release/net9.0/linux-arm64/publish/`
- .NET Client: `bin/Release/net9.0/linux-arm64/publish/`
- ESP32-S3: `.pio/build/trailer_automation_client_s3/firmware.uf2`
- Pico: `.pio/build/picow/firmware-unified.uf2`

---

**End of Summary**
