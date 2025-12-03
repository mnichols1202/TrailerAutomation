# ESP32-S3 Client Setup Guide

## What Was Added

✅ **SD Card Configuration** - Load config.json from SD card  
✅ **Device Registration** - Register with Gateway including relay info  
✅ **TCP Command Listener** - Receive commands from Gateway  
✅ **GPIO Relay Control** - Control actual GPIO pins  
✅ **Dynamic Configuration** - All settings from SD card  

## Hardware Setup

### 1. SD Card Module Wiring

```
SD Module Pin → ESP32-S3 Pin
────────────────────────────
VCC  → 3.3V
GND  → GND
MOSI → GPIO 11
MISO → GPIO 13
SCK  → GPIO 12
CS   → GPIO 10
```

**Important:**
- Use **3.3V**, NOT 5V!
- Module has built-in level shifters
- Double-check wiring before powering on

### 2. Relay Wiring (Optional)

If using relays, connect to GPIOs specified in config.json:
- Default: GPIO 4 and GPIO 5
- Can be any available GPIO pins

### 3. SHT31 Sensor (Already configured)

I2C on standard ESP32-S3 pins

## SD Card Setup

### 1. Format SD Card

- Format as **FAT32**
- Use SD card <= 32GB (FAT32 limit)

### 2. Create config.json

Copy `config.json.template` to SD card root as `config.json`

**Edit to match your setup:**

```json
{
  "Device": {
    "DeviceId": "esp32-s3-001",        ← Change this!
    "ClientId": "esp32-s3-001",         ← Match DeviceId
    "DeviceType": "ESP32-S3",
    "FriendlyName": "ESP32-S3 Sensor Node",  ← Give it a name
    "CommandListenerPort": 8888
  },
  "Intervals": {
    "HeartbeatSeconds": 60,
    "SensorReadingSeconds": 30
  },
  "Hardware": {
    "Relays": [
      {
        "Id": "relay1",
        "Name": "Relay 1",             ← Change name
        "Pin": 4,                       ← GPIO pin number
        "InitialState": false,          ← true=ON, false=OFF on boot
        "Enabled": true
      },
      {
        "Id": "relay2",
        "Name": "Relay 2",
        "Pin": 5,
        "InitialState": false,
        "Enabled": true
      }
    ]
  }
}
```

**To add more relays:**
```json
{
  "Id": "relay3",
  "Name": "My Third Relay",
  "Pin": 6,
  "InitialState": false,
  "Enabled": true
}
```

**To disable a relay without deleting:**
```json
"Enabled": false
```

### 3. Insert SD Card

- Insert SD card into module
- Power off ESP32-S3 first!

## Software Setup

### 1. Update WiFi Credentials

Edit `src/config.h`:
```cpp
#define WIFI_SSID     "YourNetworkName"
#define WIFI_PASSWORD "YourPassword"
```

### 2. Build and Upload

```bash
pio run --target upload
```

Or use PlatformIO IDE Upload button.

### 3. Monitor Serial Output

```bash
pio device monitor
```

Watch for:
```
TrailerAutomationClientS3 starting...
Initializing SD card...
SD card initialized successfully
Reading config.json...
Configuration loaded successfully:
  DeviceId: esp32-s3-001
  Relays: 2
    [relay1] Relay 1 - Pin:4 Init:OFF
    [relay2] Relay 2 - Pin:5 Init:OFF
```

## Expected Boot Sequence

```
1. TrailerAutomationClientS3 starting...
2. SD card initialization
3. Config.json loaded
4. Relay controller initialized
5. 15-second boot delay (battery-safe)
6. WiFi connecting...
7. WiFi connected (IP address shown)
8. mDNS started
9. Gateway discovered
10. Sending initial heartbeat...
11. Heartbeat OK
12. [DeviceRegistration] Registering with gateway...
13. [DeviceRegistration] Success
14. Command listener started on port 8888
15. [LED: Green] Ready!
```

## Testing

### 1. Check Gateway

Open Gateway web UI: `http://gateway-ip:5000/devices`

You should see your ESP32-S3 with:
- Device ID
- IP address
- Command port: 8888
- Relay buttons (dynamically generated from config!)

### 2. Test Relay Control

Click relay buttons in Gateway UI.

**ESP32-S3 Console:**
```
[CommandListener] Client connected from 192.168.x.x
[CommandListener] Received: {"commandId":"...","type":"setRelay","payload":{"relayId":"relay1","state":"on"}}
Relay [relay1] Relay 1 on pin 4 -> ON
[CommandListener] Sent: {"commandId":"...","success":true,"message":"Relay 'relay1' set to on"}
```

**LED/Relay should activate!**

## Troubleshooting

### SD Card Errors

**"SD card initialization failed"**
- Check wiring (especially CS pin)
- Verify 3.3V power
- Try different SD card
- Format as FAT32

**"Failed to open /config.json"**
- File must be named exactly `config.json`
- Must be in root directory (not in folder)
- Check file wasn't saved as `config.json.txt`

**"Failed to parse config.json"**
- Validate JSON syntax at jsonlint.com
- Check for missing commas, braces
- No comments allowed in JSON

### WiFi/Gateway Issues

**"Wi-Fi connection timeout"**
- Check SSID/password in config.h
- Ensure 2.4GHz network (ESP32 doesn't support 5GHz)

**"Gateway not known"**
- Ensure Gateway is running
- Check same network as ESP32
- Try rebooting both devices

**"Device registration failed"**
- Check Gateway console for errors
- Verify Gateway is on port 5000
- Check firewall not blocking

### Command Listener Issues

**Commands not working**
- Check port 8888 not blocked by firewall
- Verify ESP32 IP matches what Gateway sees
- Check serial console for [CommandListener] messages

### Relay Issues

**Relay not responding**
- Verify GPIO pin number in config.json
- Check GPIO pin supports output (avoid input-only pins)
- Test with multimeter (3.3V when ON, 0V when OFF)

**Wrong relay activates**
- Check Pin numbers in config.json
- Verify wiring matches config

## Pin Usage

### Reserved Pins (ESP32-S3)
- GPIO 0: Boot button (don't use)
- GPIO 19, 20: USB (don't use)
- GPIO 43, 44: UART0/Console (don't use)

### SD Card (SPI)
- GPIO 10: CS
- GPIO 11: MOSI
- GPIO 12: SCK
- GPIO 13: MISO

### I2C (SHT31 Sensor)
- GPIO 8: SDA (default)
- GPIO 9: SCL (default)

### RGB LED (NeoPixel)
- GPIO 48: Data (ESP32-S3-DevKitC-1)

### Available for Relays
- GPIO 1-7, 14-18, 21, 35-42, 45-47
- Recommended: GPIO 4, 5, 6, 7 (easy access on devkit)

## Advanced Configuration

### Multiple Devices

Give each device a unique DeviceId in config.json:
```
Device 1: "esp32-s3-bedroom"
Device 2: "esp32-s3-kitchen"
Device 3: "esp32-s3-garage"
```

### Custom Intervals

Adjust in config.json:
```json
"HeartbeatSeconds": 30,        ← More frequent updates
"SensorReadingSeconds": 15     ← More frequent sensor data
```

**Note:** Shorter intervals = more battery drain

### Disable Relays

Set `"Enabled": false` to keep relay in config but not use it.

## LED Status Indicators

- **Red (blinking)**: Boot delay / Config loading
- **Blue (solid)**: WiFi connecting
- **Blue (blinking)**: WiFi errors (see blink count)
- **Red (1 blink)**: mDNS/Gateway discovery failed
- **Red (2 blinks)**: Heartbeat failed
- **Red (3 blinks)**: Sensor send failed
- **Green (5 seconds)**: Connected and ready
- **Off**: Normal operation (saves power)

## Next Steps

1. Wire up relays to actual loads (lights, fans, etc.)
2. Add more relays as needed (up to 4 in config)
3. Deploy to RV/trailer
4. Monitor via Gateway web UI
5. Enjoy remote control! 🎉
