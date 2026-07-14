# ESP32-S3 Client Setup Guide

## What Was Added

✅ **LittleFS Configuration** - Load config.json from internal flash (LittleFS)  
✅ **Device Registration** - Register with Gateway including relay info  
✅ **TCP Command Listener** - Receive commands from Gateway  
✅ **GPIO Relay Control** - Control actual GPIO pins  
✅ **Dynamic Configuration** - All settings from config.json on LittleFS  

## Hardware Setup

> **Note:** The S3 stores `config.json` in internal flash via **LittleFS** — no SD card
> module or SPI wiring is required.

### 1. Relay Wiring (Optional)

If using relays, connect to GPIOs specified in config.json:
- Default: GPIO 4 and GPIO 5
- Can be any available GPIO pins

### 2. SHT31 Sensor (Already configured)

I2C on standard ESP32-S3 pins

## Config Setup (LittleFS)

### 1. Edit config.json

Edit `data/config.json` (copy from `data/config.json.template` if starting fresh). It is
uploaded to the S3's internal flash with `pio run --target uploadfs` (see below).

**Edit to match your setup:**

```json
{
  "WiFi": {
    "SSID": "YourNetworkName",          ← 2.4GHz network only
    "Password": "YourPassword"
  },
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

## Software Setup

### 1. WiFi Credentials

WiFi credentials live in the `WiFi` section of `data/config.json` (shown above) — **not**
in `src/config.h`. Edit them there before uploading the filesystem.

### 2. Build and Upload

The S3 supports two-pass flashing (order doesn't matter, config persists across firmware
updates):

```bash
pio run --target uploadfs    # upload config.json to LittleFS
pio run --target upload      # upload firmware
```

Or use the PlatformIO IDE "Upload Filesystem Image" and "Upload" buttons.

### 3. Monitor Serial Output

```bash
pio device monitor
```

Watch for:
```
TrailerAutomationClientS3 starting...
Initializing LittleFS...
LittleFS initialized successfully
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
2. LittleFS mount
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

### Filesystem / Config Errors

**"LittleFS mount failed"**
- Re-upload the filesystem: `pio run --target uploadfs`
- Confirm `board_build.filesystem = littlefs` in `platformio.ini`

**"Failed to open /config.json"**
- Make sure `data/config.json` exists, then run `pio run --target uploadfs`
- File must be named exactly `config.json`

**"Failed to parse config.json"**
- Validate JSON syntax at jsonlint.com
- Check for missing commas, braces
- No comments allowed in JSON

### WiFi/Gateway Issues

**"Wi-Fi connection timeout"**
- Check SSID/password in the `WiFi` section of `data/config.json` (re-run `uploadfs` after editing)
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
