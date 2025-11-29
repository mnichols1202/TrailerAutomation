# Quick Start Guide - TrailerAutomationClientPico

This guide will help you get your Pico 2 W up and running quickly.

## Step 1: Hardware Setup

### Required Components
- Raspberry Pi Pico 2 W (or Pico W)
- SHT31 Temperature/Humidity Sensor
- Breadboard and jumper wires
- USB cable (Micro-USB)

### Wiring

Connect your SHT31 sensor to the Pico 2 W:

```
Pico 2 W Pin    →   SHT31 Pin
────────────────────────────────
Pin 36 (3.3V)   →   VCC
Pin 38 (GND)    →   GND
Pin 1  (GP0)    →   SDA
Pin 2  (GP1)    →   SCL
```

Visual diagram:
```
        Pico 2 W                    SHT31
    ┌─────────────┐              ┌────────┐
    │             │              │  SHT31 │
    │   3.3V  ○───┼──────────────┤ VCC    │
    │   GP0   ○───┼──────────────┤ SDA    │
    │   GP1   ○───┼──────────────┤ SCL    │
    │   GND   ○───┼──────────────┤ GND    │
    │             │              └────────┘
    └─────────────┘
```

## Step 2: Install MicroPython

### Download MicroPython
1. Go to https://micropython.org/download/rp2-pico-w/
2. Download the latest `.uf2` file (1.26 or newer)

### Flash to Pico
1. Hold the BOOTSEL button on your Pico
2. Connect USB cable to computer (while holding BOOTSEL)
3. Release BOOTSEL - Pico appears as USB drive (RPI-RP2)
4. Drag and drop the `.uf2` file onto the drive
5. Pico automatically reboots with MicroPython

## Step 3: Install Thonny IDE

### Download and Install
1. Go to https://thonny.org/
2. Download and install Thonny for your OS
3. Launch Thonny

### Configure for Pico
1. Click bottom-right corner (interpreter selector)
2. Select **"MicroPython (Raspberry Pi Pico)"**
3. Select your Pico's COM port
4. You should see `>>>` prompt in Shell

## Step 4: Configure WiFi

1. In Thonny, open `config.py`
2. Edit WiFi settings:
   ```python
   WIFI_SSID = "YourNetworkName"
   WIFI_PASSWORD = "YourPassword123"
   ```
3. Customize other settings if needed (optional):
   ```python
   FRIENDLY_NAME = "Pico2W-Workshop"  # Give it a name
   I2C_SDA_PIN = 0  # Change if using different pins
   I2C_SCL_PIN = 1
   ```
4. Save the file

## Step 5: Upload Files to Pico

### Using Thonny (Recommended)

For each Python file:

1. Open the file in Thonny (`main.py`, `config.py`, etc.)
2. Click **File → Save As...**
3. Select **"Raspberry Pi Pico"** (not "This computer")
4. Keep the same filename
5. Click **OK**

Files to upload:
- ✅ `config.py` (MUST BE FIRST!)
- ✅ `gateway_discovery.py`
- ✅ `sht31_reader.py`
- ✅ `main.py` (upload LAST)

Optional test files:
- `test_wifi.py`
- `test_i2c_scan.py`
- `test_sht31.py`
- `test_discovery.py`

## Step 6: Test Each Component

### Test 1: I2C Sensor Detection

In Thonny Shell, run:
```python
import test_i2c_scan
```

Expected output:
```
Found 1 device(s):
  Address: 0x44 (68) - SHT31 (Temp/Humidity)
✓ SHT31/SHT3x detected!
```

If sensor not found:
- Check wiring
- Try `scan_all_buses()` to test different pins
- Verify 3.3V power

### Test 2: WiFi Connection

In Thonny Shell, run:
```python
import test_wifi
```

Expected output:
```
CONNECTION SUCCESSFUL!
IP Address:  192.168.1.50
Subnet Mask: 255.255.255.0
Gateway:     192.168.1.1
```

If connection fails:
- Verify SSID and password in `config.py`
- Ensure 2.4GHz network (not 5GHz)
- Check WiFi signal strength

### Test 3: SHT31 Readings

In Thonny Shell, run:
```python
import test_sht31
```

Expected output:
```
Reading  1: Temp: 22.45°C (72.41°F) | Humidity: 45.67%
Reading  2: Temp: 22.46°C (72.43°F) | Humidity: 45.65%
...
```

### Test 4: Gateway Discovery

In Thonny Shell, run:
```python
import test_discovery
```

Expected output:
```
DISCOVERY SUCCESSFUL!
Gateway URL: http://192.168.1.100:5000
```

If discovery fails:
- Ensure TrailerAutomationGateway is running
- Check both devices on same network
- Verify mDNS is enabled on gateway

## Step 7: Run the Main Program

### Manual Start

In Thonny Shell:
```python
import main
```

You should see:
```
TrailerAutomationClientPico starting...
Connecting to WiFi: MyNetwork
Connected! IP: 192.168.1.50
Discovering TrailerAutomationGateway...
Gateway discovered: http://192.168.1.100:5000
Starting heartbeat loop...
[...] Heartbeat OK: {"status":"ok"}
[...] Local sensor reading: TempC=22.45 Humidity=45.67
```

### Automatic Start on Boot

The Pico automatically runs `main.py` on power-up (no action needed).

To monitor:
1. Keep Thonny connected
2. Press CTRL+D to soft reboot
3. Watch Shell output

## Step 8: Deploy and Test

### Deploy
1. Disconnect USB from computer
2. Connect to power supply (USB power adapter)
3. Pico boots and runs automatically
4. LED will blink to indicate activity

### Verify on Gateway
- Check gateway logs/UI for device
- Verify heartbeats received
- Confirm sensor data arriving

## LED Status Guide

| LED Pattern | Meaning |
|-------------|---------|
| Rapid blinks during startup | WiFi connecting |
| 3 quick blinks | WiFi connected |
| Single blink every 10s | Heartbeat sent |

## Troubleshooting

### Problem: Pico won't connect to WiFi

**Solutions:**
- Verify `WIFI_SSID` and `WIFI_PASSWORD` are correct
- Ensure network is 2.4GHz (Pico doesn't support 5GHz)
- Check Pico is in WiFi range
- Try moving closer to router
- Verify network allows new device connections

### Problem: SHT31 not detected

**Solutions:**
- Run `test_i2c_scan.py` to detect I2C devices
- Check all 4 wires (VCC, GND, SDA, SCL)
- Verify 3.3V on VCC pin (measure with multimeter)
- Try swapping SDA/SCL if wired incorrectly
- Check sensor address: try `0x45` if `0x44` fails
- Some SHT31 modules need pull-up resistors

### Problem: Gateway not discovered

**Solutions:**
- Verify TrailerAutomationGateway is running
- Check both on same network (run ipconfig/ifconfig)
- Test gateway accessible: ping its IP from computer
- Increase `MDNS_DISCOVERY_TIMEOUT_SEC` in config
- Check firewall allows mDNS (UDP port 5353)
- Try restarting gateway

### Problem: Memory errors

**Solutions:**
- MicroPython has limited RAM (~200KB free)
- Increase timing intervals (reduce frequency)
- Remove test files if deployed
- Simplify code if heavily modified

### Problem: Program crashes or stops

**Solutions:**
- Check Shell for error messages
- Verify all files uploaded correctly
- Ensure config.py has valid Python syntax
- Check for typos in WIFI_SSID/PASSWORD
- Try soft reset: CTRL+D in Shell
- Hard reset: unplug and replug USB

## Advanced: Remote Monitoring

### View logs without USB

Option 1: WebREPL
```python
import webrepl_setup
# Follow prompts to enable WebREPL
```

Option 2: Network logging
- Modify code to send logs to gateway
- Add syslog support

### Update code remotely

Use `mpremote` or `rshell`:
```bash
mpremote connect COM3 fs cp main.py :main.py
```

## Next Steps

✅ Pico is now running!

- Monitor in gateway UI
- Add more sensors
- Customize friendly names
- Adjust timing intervals
- Deploy additional Picos

## Support

For issues:
1. Run all test scripts
2. Check error messages in Shell
3. Review README.md
4. Check main TrailerAutomation docs

## Quick Reference

| File | Purpose |
|------|---------|
| `main.py` | Main program (auto-runs on boot) |
| `config.py` | WiFi and device settings |
| `sht31_reader.py` | SHT31 sensor driver |
| `gateway_discovery.py` | mDNS service discovery |
| `test_*.py` | Individual component tests |

## Default Pin Assignments

| Component | Pin | GPIO |
|-----------|-----|------|
| SHT31 SDA | Pin 1 | GP0 |
| SHT31 SCL | Pin 2 | GP1 |
| Status LED | Built-in | "LED" |
| I2C Bus | I2C0 | - |

## Configuration Quick Edit

To change settings, edit `config.py`:

```python
# Essential settings
WIFI_SSID = "YourNetwork"      # Your WiFi name
WIFI_PASSWORD = "YourPass"      # Your WiFi password
FRIENDLY_NAME = "Pico-Kitchen"  # Device name

# Timing (seconds)
HEARTBEAT_INTERVAL_SEC = 10     # Heartbeat frequency
SENSOR_REPORT_INTERVAL_SEC = 30 # Sensor reading frequency

# I2C Pins (if not using defaults)
I2C_SDA_PIN = 0  # GP0
I2C_SCL_PIN = 1  # GP1
```

Good luck! 🚀
