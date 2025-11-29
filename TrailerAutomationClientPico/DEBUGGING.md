# Debugging Guide - Pico 2 W with MicroPython

Complete guide to connecting, debugging, and troubleshooting your Pico 2 W.

---

## Table of Contents
1. [Connecting to the Pico](#connecting-to-the-pico)
2. [Method 1: Thonny IDE (Easiest)](#method-1-thonny-ide-easiest)
3. [Method 2: Serial Terminal](#method-2-serial-terminal)
4. [Method 3: VS Code + MicroPico](#method-3-vs-code--micropico)
5. [Method 4: mpremote](#method-4-mpremote)
6. [Method 5: WebREPL (Wireless)](#method-5-webrepl-wireless)
7. [Interactive Debugging](#interactive-debugging)
8. [Common Issues](#common-issues)
9. [Advanced Debugging Techniques](#advanced-debugging-techniques)

---

## Connecting to the Pico

### Hardware Connection
1. Connect Pico 2 W to your computer via **USB cable**
2. Windows will detect it as a **COM port** (e.g., COM3, COM4)
3. Baud rate: **115200** (standard for Pico)

### Finding Your COM Port

**Windows (PowerShell):**
```powershell
Get-WmiObject Win32_SerialPort | Select-Object Name, DeviceID
```

**Windows (Device Manager):**
1. Press `Win + X` → Device Manager
2. Expand "Ports (COM & LPT)"
3. Look for "USB Serial Device (COM#)"

---

## Method 1: Thonny IDE (Easiest) ⭐

### Install Thonny
1. Download from https://thonny.org/
2. Install for Windows
3. Launch Thonny

### Connect to Pico
1. **Plug in Pico via USB**
2. Click **bottom-right corner** (Python version)
3. Select **"MicroPython (Raspberry Pi Pico)"**
4. Select your **COM port**
5. Click **"OK"**

### Debug Features

#### View Live Output
```python
# Shell window shows all print() statements in real-time
>>> import main
TrailerAutomationClientPico starting...
Connecting to WiFi: MM0001
Connected! IP: 192.168.1.50
...
```

#### Interactive REPL
```python
>>> # Test individual functions
>>> from config import Config
>>> config = Config()
>>> print(config.WIFI_SSID)
MM0001

>>> # Test WiFi
>>> import network
>>> wlan = network.WLAN(network.STA_IF)
>>> wlan.active(True)
>>> wlan.isconnected()
True
>>> wlan.ifconfig()
('192.168.1.50', '255.255.255.0', '192.168.1.1', '192.168.1.1')

>>> # Test sensor
>>> from sht31_reader import Sht31Reader
>>> sensor = Sht31Reader(i2c_id=0, sda_pin=0, scl_pin=1)
>>> temp, humid = sensor.read_measurement()
>>> print(f"Temp: {temp:.2f}°C, Humidity: {humid:.2f}%")
Temp: 22.45°C, Humidity: 45.67%
```

#### Stop Running Program
- **CTRL + C** - Stop current execution
- **CTRL + D** - Soft reboot (restarts MicroPython)

#### Upload Files
1. **File → Open** → Select file from computer
2. **File → Save As** → Choose "Raspberry Pi Pico"
3. Enter filename (keep same name)
4. Click OK

#### View Files on Pico
1. **View → Files** (opens file browser)
2. Left pane: Your computer
3. Right pane: Pico filesystem
4. Drag & drop to upload/download

---

## Method 2: Serial Terminal

### Using PuTTY (Windows)

#### Install PuTTY
Download from: https://www.putty.org/

#### Connect
1. Launch PuTTY
2. **Connection Type:** Serial
3. **Serial line:** COM3 (your port)
4. **Speed:** 115200
5. Click **"Open"**

#### Settings
In PuTTY Configuration:
- **Terminal → Keyboard:** Set backspace to "Control-H"
- **Terminal → Line discipline:** Force on
- **Window → Translation:** UTF-8

### Using Windows Terminal

#### Install
```powershell
winget install Microsoft.WindowsTerminal
```

#### Connect
```powershell
# Find your COM port first
Get-WmiObject Win32_SerialPort | Select-Object DeviceID

# Connect (replace COM3 with your port)
mode COM3: BAUD=115200 PARITY=N DATA=8
python -m serial.tools.miniterm COM3 115200
```

Or use PowerShell directly:
```powershell
$port = new-Object System.IO.Ports.SerialPort COM3,115200,None,8,one
$port.Open()
while($true) {
    if ($port.BytesToRead) {
        $port.ReadExisting()
    }
    Start-Sleep -Milliseconds 100
}
```

---

## Method 3: VS Code + MicroPico

**MicroPico v4.3.4+** (formerly Pico-W-Go)

### Install VS Code Extension

1. Open **VS Code**
2. Press **CTRL + SHIFT + X** (Extensions)
3. Search for **"MicroPico"** by paulober
4. Click **Install**

### Configure

1. Open your project folder
2. Press **CTRL + SHIFT + P**
3. Type **"MicroPico: Connect"** (or click status bar)
4. Follow prompts

### Key Commands

- **CTRL + SHIFT + P** → "Connect"
- **CTRL + SHIFT + P** → "Upload project to Pico"
- **CTRL + SHIFT + P** → "Run current file on Pico"
- **CTRL + SHIFT + P** → "Upload file to Pico"

### Terminal Access
- View → Terminal
- Interactive REPL available
- Real-time output from print()

---

## Method 4: mpremote

### Install
```powershell
pip install mpremote
```

### Connect and Run
```powershell
# Connect to Pico (auto-detects)
mpremote connect list

# Run a file directly
mpremote run main.py

# Interactive REPL
mpremote repl

# Execute Python command
mpremote exec "import machine; print(machine.unique_id())"

# Copy file to Pico
mpremote fs cp config.py :config.py

# List files on Pico
mpremote fs ls

# View file on Pico
mpremote fs cat main.py

# Remove file
mpremote fs rm old_file.py
```

### Monitor Live Output
```powershell
# Run and see output
mpremote run main.py

# Or mount Pico and run
mpremote mount . exec "import main"
```

---

## Method 5: WebREPL (Wireless) 📡

### Setup WebREPL (One-Time)

Connect via USB first, then:

```python
>>> import webrepl_setup
```

Follow prompts:
1. Enable WebREPL? **E** (Enable)
2. Set password (at least 4 characters)
3. Reboot? **y** (Yes)

### Configure WiFi for WebREPL

Edit `boot.py` on the Pico:
```python
# boot.py
import network
import webrepl

wlan = network.WLAN(network.STA_IF)
wlan.active(True)
wlan.connect('MM0001', 'S3cur1ty3742')  # Your WiFi

webrepl.start()
```

Upload this file to the Pico.

### Connect via WebREPL

**Option A: Web Browser**
1. Go to: http://micropython.org/webrepl/
2. Enter `ws://192.168.1.50:8266/` (Pico's IP)
3. Click **Connect**
4. Enter password
5. Use REPL!

**Option B: webrepl_cli.py**
```powershell
# Download webrepl_cli.py from GitHub
curl -o webrepl_cli.py https://raw.githubusercontent.com/micropython/webrepl/master/webrepl_cli.py

# Upload file
python webrepl_cli.py -p YourPassword config.py 192.168.1.50:config.py

# Download file
python webrepl_cli.py -p YourPassword 192.168.1.50:main.py main_backup.py
```

**Advantages:**
- No USB cable needed!
- Upload/download files wirelessly
- Interactive debugging over WiFi

---

## Interactive Debugging

### Using REPL for Step-by-Step Testing

```python
>>> # 1. Test imports
>>> import network
>>> import machine
>>> from config import Config
>>> 
>>> # 2. Check configuration
>>> config = Config()
>>> print(f"SSID: {config.WIFI_SSID}")
SSID: MM0001
>>> 
>>> # 3. Test WiFi connection
>>> wlan = network.WLAN(network.STA_IF)
>>> wlan.active(True)
>>> wlan.connect(config.WIFI_SSID, config.WIFI_PASSWORD)
>>> # Wait a moment...
>>> wlan.isconnected()
True
>>> print(wlan.ifconfig())
('192.168.1.50', '255.255.255.0', '192.168.1.1', '192.168.1.1')
>>> 
>>> # 4. Test I2C scan
>>> i2c = machine.I2C(0, sda=machine.Pin(0), scl=machine.Pin(1))
>>> devices = i2c.scan()
>>> print([hex(d) for d in devices])
['0x44']
>>> 
>>> # 5. Test sensor
>>> from sht31_reader import Sht31Reader
>>> sensor = Sht31Reader()
>>> temp, humid = sensor.read_measurement()
>>> print(f"{temp:.2f}°C, {humid:.2f}%")
22.45°C, 45.67%
>>> 
>>> # 6. Test gateway discovery
>>> from gateway_discovery import discover_gateway
>>> gateway = discover_gateway(timeout_sec=8)
>>> print(gateway)
http://192.168.1.100:5000
>>> 
>>> # 7. Test HTTP request
>>> import urequests as requests
>>> import json
>>> payload = {"ClientId": "test", "DeviceType": "Pico2W", "FriendlyName": "Test"}
>>> response = requests.post(
...     f"{gateway}/api/heartbeat",
...     data=json.dumps(payload),
...     headers={"Content-Type": "application/json"}
... )
>>> print(response.status_code)
200
>>> print(response.text)
{"status":"ok"}
>>> response.close()
```

### Print Debugging

Add detailed print statements:

```python
# In main.py
def send_heartbeat(gateway_url, client_id, device_type, friendly_name):
    print(f"[DEBUG] send_heartbeat called")
    print(f"[DEBUG] gateway_url: {gateway_url}")
    print(f"[DEBUG] client_id: {client_id}")
    
    try:
        payload = {
            "ClientId": client_id,
            "DeviceType": device_type,
            "FriendlyName": friendly_name
        }
        print(f"[DEBUG] Payload created: {payload}")
        
        url = f"{gateway_url}/api/heartbeat"
        print(f"[DEBUG] Full URL: {url}")
        
        response = requests.post(url, data=json.dumps(payload), headers={"Content-Type": "application/json"})
        print(f"[DEBUG] Response code: {response.status_code}")
        print(f"[DEBUG] Response text: {response.text}")
        
        response.close()
        print(f"[DEBUG] Response closed")
        return True
        
    except Exception as e:
        print(f"[DEBUG] Exception caught: {type(e).__name__}")
        print(f"[DEBUG] Exception message: {e}")
        import sys
        sys.print_exception(e)
        return False
```

### Memory Debugging

```python
>>> import gc
>>> 
>>> # Check free memory
>>> gc.mem_free()
184320
>>> 
>>> # Check allocated memory
>>> gc.mem_alloc()
79680
>>> 
>>> # Force garbage collection
>>> gc.collect()
>>> gc.mem_free()
186432
>>> 
>>> # Monitor memory during operation
>>> def monitor_memory():
...     while True:
...         print(f"Free: {gc.mem_free()} bytes")
...         time.sleep(5)
```

---

## Common Issues

### Issue 1: Can't Connect to Pico

**Symptoms:**
- No COM port appears
- Device not recognized

**Solutions:**
```powershell
# Check if Pico is detected
Get-PnpDevice | Where-Object {$_.FriendlyName -like "*USB*"}

# Try different USB cable (data cable, not charge-only)
# Try different USB port
# Hold BOOTSEL while plugging in to reset
```

### Issue 2: REPL Not Responding

**Symptoms:**
- No `>>>` prompt
- Keyboard input doesn't work

**Solutions:**
- Press **CTRL + C** (interrupt)
- Press **CTRL + D** (soft reboot)
- Unplug and replug USB
- In Thonny: **Stop/Restart backend**

### Issue 3: Program Stuck/Frozen

**Symptoms:**
- No output
- Can't interrupt

**Solutions:**
```python
# Hard reset from REPL (if accessible)
>>> import machine
>>> machine.reset()

# Or hold BOOTSEL + press RUN button
# Or unplug power
```

### Issue 4: Import Errors

**Symptoms:**
```
ImportError: no module named 'config'
```

**Solutions:**
```python
# Check files on Pico
>>> import os
>>> os.listdir()
['main.py', 'boot.py']  # config.py is missing!

# Upload missing files via Thonny or mpremote
```

### Issue 5: Memory Errors

**Symptoms:**
```
MemoryError: memory allocation failed
```

**Solutions:**
```python
# Check free memory
>>> import gc
>>> gc.mem_free()
12800  # Very low!

# Force garbage collection
>>> gc.collect()
>>> gc.mem_free()
145600  # Better!

# Close HTTP responses
response.close()

# Delete large variables
del large_buffer
gc.collect()
```

---

## Advanced Debugging Techniques

### 1. Exception Tracebacks

```python
try:
    # Your code
    risky_function()
except Exception as e:
    import sys
    sys.print_exception(e)  # Full traceback
```

### 2. Conditional Debugging

```python
DEBUG = True  # Set in config.py

def debug_print(msg):
    if DEBUG:
        print(f"[DEBUG] {msg}")

debug_print("WiFi connecting...")
```

### 3. Timing Analysis

```python
import time

start = time.ticks_ms()
# Your code
sensor.read_measurement()
elapsed = time.ticks_diff(time.ticks_ms(), start)
print(f"Sensor read took {elapsed}ms")
```

### 4. Network Traffic Inspection

```python
# Log all HTTP requests
def debug_post(url, **kwargs):
    print(f"POST {url}")
    print(f"Headers: {kwargs.get('headers')}")
    print(f"Data: {kwargs.get('data')}")
    response = requests.post(url, **kwargs)
    print(f"Response: {response.status_code}")
    return response
```

### 5. LED-Based Debugging

```python
import machine
led = machine.Pin("LED", machine.Pin.OUT)

def blink_code(count):
    """Blink LED for error codes"""
    for _ in range(count):
        led.on()
        time.sleep_ms(200)
        led.off()
        time.sleep_ms(200)

# WiFi failed: 2 blinks
# Gateway failed: 3 blinks
# Sensor failed: 4 blinks
```

### 6. File Logging

```python
def log_to_file(message):
    with open("debug.log", "a") as f:
        f.write(f"{time.time()}: {message}\n")

# View later
>>> with open("debug.log") as f:
...     print(f.read())
```

### 7. Remote Logging (to Gateway)

```python
def send_debug_log(gateway_url, message):
    try:
        requests.post(
            f"{gateway_url}/api/debug",
            data=json.dumps({"message": message}),
            headers={"Content-Type": "application/json"}
        ).close()
    except:
        pass  # Don't let logging break the app
```

---

## Debugging Workflow

### Quick Debug Session

1. **Connect via Thonny**
2. **Stop running code:** CTRL + C
3. **Test component:**
   ```python
   >>> import test_wifi
   >>> import test_i2c_scan
   >>> import test_sht31
   ```
4. **Fix issues**
5. **Upload fixed file**
6. **Test again:** 
   ```python
   >>> import main
   ```
7. **Monitor output**

### Full Debug Session

```python
# 1. Connect and stop
CTRL + C

# 2. Check system status
>>> import gc, machine
>>> print(f"Free RAM: {gc.mem_free()}")
>>> print(f"Unique ID: {machine.unique_id().hex()}")

# 3. Test network
>>> import network
>>> wlan = network.WLAN(network.STA_IF)
>>> print(wlan.isconnected())
>>> print(wlan.ifconfig())

# 4. Test hardware
>>> import machine
>>> i2c = machine.I2C(0, sda=machine.Pin(0), scl=machine.Pin(1))
>>> print(i2c.scan())

# 5. Test individual modules
>>> import test_wifi
>>> import test_sht31
>>> import test_discovery

# 6. Run main with debugging
>>> import main
# Watch output...

# 7. If error occurs, get traceback
>>> import sys
>>> sys.print_exception(last_exception)
```

---

## Best Practices

### DO ✅
- Use `print()` liberally during development
- Test components individually before full integration
- Close all HTTP responses (`response.close()`)
- Call `gc.collect()` periodically in long-running code
- Keep test scripts on the Pico for troubleshooting
- Use descriptive variable names in debug output
- Add timestamps to debug messages

### DON'T ❌
- Don't leave infinite loops without escapes
- Don't allocate large buffers unnecessarily
- Don't forget to handle exceptions
- Don't mix tabs and spaces (use spaces only)
- Don't leave debug code in production
- Don't forget to close resources

---

## Quick Reference Card

| Action | Thonny | Serial Terminal | mpremote |
|--------|--------|-----------------|----------|
| **Connect** | Select interpreter | Open COM port | `mpremote` |
| **Stop** | CTRL + C | CTRL + C | CTRL + C |
| **Reboot** | CTRL + D | CTRL + D | `mpremote reset` |
| **Upload** | File → Save As | N/A | `mpremote fs cp` |
| **REPL** | Built-in | Built-in | `mpremote repl` |
| **Run** | F5 | `import main` | `mpremote run` |
| **List Files** | View → Files | `os.listdir()` | `mpremote fs ls` |

---

## Emergency Recovery

### Pico Won't Boot / Corrupted Filesystem

1. **Enter BOOTSEL mode:**
   - Unplug Pico
   - Hold BOOTSEL button
   - Plug in USB
   - Release BOOTSEL
   - Pico appears as USB drive

2. **Re-flash MicroPython:**
   - Download fresh `.uf2` file
   - Drag to RPI-RP2 drive
   - Wait for reboot

3. **Re-upload files**

### Factory Reset

```python
>>> import os
>>> # Delete all files
>>> for f in os.listdir():
...     if f not in ['boot.py']:  # Keep boot.py
...         os.remove(f)
>>> 
>>> # Soft reboot
>>> import machine
>>> machine.reset()
```

---

## Summary: Recommended Setup

**For Development:** Use **Thonny** ⭐
- Easy to use
- Great for beginners
- Built-in file manager
- Interactive REPL
- One-click upload

**For Production:** Use **mpremote** or **WebREPL** 📡
- Command-line efficiency
- Scriptable uploads
- Wireless access (WebREPL)
- CI/CD integration

**For Quick Checks:** Use **Serial Terminal**
- Fast connection
- Minimal overhead
- Works everywhere

---

Happy debugging! 🐛🔧
