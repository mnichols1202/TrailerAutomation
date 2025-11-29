"""
TrailerAutomationClientPico - MicroPython 1.26 for Pico 2 W
Main entry point for the trailer automation client.
"""

print("\n" + "="*60)
print("Starting TrailerAutomationClientPico...")
print("="*60)

import time
import json
import machine
import network
import _thread

print("Core imports successful")

# Try importing urequests (needs to be installed via mip)
try:
    import urequests as requests
    print("urequests imported successfully")
except ImportError as e:
    print(f"ERROR: urequests not installed!")
    print("To install, run in REPL:")
    print("  import mip")
    print('  mip.install("urequests")')
    raise

from gateway_discovery import discover_gateway
from sht31_reader import Sht31Reader
import config

print("All imports successful!")

# Status LED on Pico 2 W
led = machine.Pin("LED", machine.Pin.OUT)

def blink_led(times=1, delay_ms=100):
    """Blink the onboard LED."""
    for _ in range(times):
        led.on()
        time.sleep_ms(delay_ms)
        led.off()
        time.sleep_ms(delay_ms)

def connect_wifi():
    """Connect to WiFi network - using test_wifi.py working approach."""
    print("\n=== WiFi Connection ===")
    
    # Note: rp2.country() breaks WiFi scanning on this build - do not use!
    
    # Proper WiFi initialization for Pico 2W (same as test_wifi.py)
    wlan = network.WLAN(network.STA_IF)
    
    # Clean state - deactivate first
    wlan.active(False)
    time.sleep(0.5)
    
    # Activate with proper timing
    wlan.active(True)
    time.sleep(2)  # Give CYW43 chip time to initialize
    
    print(f"WLAN activated: {wlan.active()}")
    
    # Apply critical WiFi optimizations
    try:
        wlan.config(pm=0xa11140)  # CRITICAL: Disable power management
        print("✓ Power management: DISABLED")
    except Exception as e:
        print(f"! Power management: {e}")
    
    # Note: country config not supported in this MicroPython build
    
    try:
        wlan.config(txpower=20)
        print("✓ TX Power: 20 dBm")
    except Exception as e:
        print(f"! TX Power: {e}")
    
    try:
        wlan.config(hostname='Pico2W-Trailer')
        print("✓ Hostname: Pico2W-Trailer")
    except Exception as e:
        print(f"! Hostname: {e}")
    
    if wlan.isconnected():
        print("\nAlready connected!")
        ifconfig = wlan.ifconfig()
        print(f"IP: {ifconfig[0]}")
        return True
    
    # Scan for networks
    print(f"\nScanning for networks...")
    try:
        networks = wlan.scan()
        print(f"Found {len(networks)} networks")
        
        target_found = False
        for net in networks:
            ssid = net[0].decode('utf-8')
            rssi = net[3]
            channel = net[2]
            
            is_target = (ssid == config.WIFI_SSID)
            if is_target:
                target_found = True
                print(f">>> {ssid} {rssi}dBm Ch:{channel} <<< TARGET")
            
        if not target_found:
            print(f"! Target SSID '{config.WIFI_SSID}' NOT FOUND")
            print("  Attempting connection anyway...")
            
    except Exception as e:
        print(f"✗ Scan error: {e}")
        print("  WiFi chip may need reset (unplug USB, wait 10s)")
        return False
    
    # Connect
    print(f"\nConnecting to '{config.WIFI_SSID}'...")
    try:
        wlan.connect(config.WIFI_SSID, config.WIFI_PASSWORD)
    except Exception as e:
        print(f"✗ Connection failed: {e}")
        return False
    
    # Wait for connection
    max_wait = 30
    print("Monitoring connection (30s timeout)...")
    
    status_names = {
        -3: "BAD_AUTH", -2: "TIMEOUT", -1: "GENERIC_FAIL",
        0: "IDLE", 1: "CONNECTING", 2: "WRONG_PASSWORD",
        3: "NO_AP_FOUND", 4: "CONNECT_FAIL", 5: "GOT_IP"
    }
    
    last_status = None
    while max_wait > 0:
        status = wlan.status()
        status_str = status_names.get(status, f"UNKNOWN({status})")
        
        if status != last_status or max_wait % 5 == 0:
            print(f"  [{max_wait:2}s] {status_str}")
            last_status = status
        
        if wlan.isconnected():
            print(f"  [{max_wait:2}s] {status_str} - SUCCESS!\n")
            break
        
        # Fatal errors
        if status in [-3, 2]:
            print(f"\n! ERROR: {status_str} - Wrong password or auth issue")
            return False
        elif status in [3, 4]:
            print(f"\n! ERROR: {status_str} - Network not found or connection failed")
            return False
        
        max_wait -= 1
        time.sleep(1)
    
    if wlan.isconnected():
        ifconfig = wlan.ifconfig()
        print("=" * 50)
        print("✓✓✓ CONNECTED! ✓✓✓")
        print("=" * 50)
        print(f"IP:      {ifconfig[0]}")
        print(f"Gateway: {ifconfig[2]}")
        print(f"DNS:     {ifconfig[3]}")
        return True
    else:
        print(f"\n! Connection TIMEOUT")
        return False

def send_heartbeat(gateway_url, client_id, device_type, friendly_name):
    """Send heartbeat to gateway."""
    try:
        payload = {
            "ClientId": client_id,
            "DeviceType": device_type,
            "FriendlyName": friendly_name
        }
        
        headers = {"Content-Type": "application/json"}
        url = f"{gateway_url}/api/heartbeat"
        
        response = requests.post(url, data=json.dumps(payload), headers=headers)
        
        if response.status_code == 200:
            print(f"[{time.localtime()}] Heartbeat OK: {response.text}")
            blink_led(1, 50)
        else:
            print(f"[{time.localtime()}] Heartbeat failed: HTTP {response.status_code}")
        
        response.close()
        return True
    except Exception as e:
        print(f"[{time.localtime()}] Heartbeat error: {e}")
        return False

def send_sensor_reading(gateway_url, client_id, temperature_c, humidity_percent):
    """Send sensor reading to gateway."""
    try:
        payload = {
            "ClientId": client_id,
            "TemperatureC": temperature_c,
            "HumidityPercent": humidity_percent
        }
        
        headers = {"Content-Type": "application/json"}
        url = f"{gateway_url}/api/sensor-readings"
        
        response = requests.post(url, data=json.dumps(payload), headers=headers)
        
        if response.status_code == 200:
            print(f"[{time.localtime()}] Sensor reading sent: {response.text}")
        else:
            print(f"[{time.localtime()}] Sensor reading failed: HTTP {response.status_code}")
        
        response.close()
        return True
    except Exception as e:
        print(f"[{time.localtime()}] Sensor reading error: {e}")
        return False

def sensor_loop(gateway_url, client_id):
    """Background thread for reading and sending sensor data."""
    print("Starting sensor loop...")
    
    try:
        # Initialize SHT31 sensor (I2C0, default pins for Pico: SDA=GP0, SCL=GP1)
        sensor = Sht31Reader(
            i2c_id=config.I2C_BUS,
            sda_pin=config.I2C_SDA_PIN,
            scl_pin=config.I2C_SCL_PIN,
            address=config.SHT31_ADDRESS
        )
        
        print("SHT31 sensor initialized")
        
        while True:
            try:
                # Read sensor
                temperature_c, humidity_percent = sensor.read_measurement()
                
                print(f"[{time.localtime()}] Local sensor reading: "
                      f"TempC={temperature_c:.2f} Humidity={humidity_percent:.2f}")
                
                # Send to gateway
                send_sensor_reading(gateway_url, client_id, temperature_c, humidity_percent)
                
            except Exception as e:
                print(f"[{time.localtime()}] Sensor loop error: {e}")
            
            # Wait for next reading
            time.sleep(config.SENSOR_REPORT_INTERVAL_SEC)
            
    except Exception as e:
        print(f"[{time.localtime()}] Failed to initialize SHT31 sensor: {e}")

def main():
    """Main program entry point."""
    print("=" * 50)
    print("TrailerAutomationClientPico starting...")
    print(f"MicroPython version: {machine.unique_id().hex()}")
    print("=" * 50)
    
    # Connect to WiFi
    if not connect_wifi():
        print("Cannot continue without WiFi. Restarting...")
        time.sleep(5)
        machine.reset()
        return
    
    # Discover gateway via mDNS
    print("Discovering TrailerAutomationGateway via mDNS...")
    gateway_url = discover_gateway(timeout_sec=config.MDNS_DISCOVERY_TIMEOUT_SEC)
    
    if gateway_url is None:
        print("Gateway discovery failed: No TrailerAutomationGateway found.")
        print("Restarting in 10 seconds...")
        time.sleep(10)
        machine.reset()
        return
    
    print(f"Discovered Gateway: {gateway_url}")
    
    # Device identity
    client_id = config.CLIENT_ID or machine.unique_id().hex()[:16]
    device_type = config.DEVICE_TYPE
    friendly_name = config.FRIENDLY_NAME
    
    print(f"ClientId: {client_id}")
    print(f"DeviceType: {device_type}")
    print(f"FriendlyName: {friendly_name}")
    
    # Start sensor loop in background thread
    print("Starting sensor loop...")
    _thread.start_new_thread(sensor_loop, (gateway_url, client_id))
    
    # Main heartbeat loop
    print("Starting heartbeat loop...")
    while True:
        try:
            send_heartbeat(gateway_url, client_id, device_type, friendly_name)
        except Exception as e:
            print(f"[{time.localtime()}] Main loop error: {e}")
        
        # Wait for next heartbeat
        time.sleep(config.HEARTBEAT_INTERVAL_SEC)

# Auto-run on boot
# In MicroPython, main.py is automatically imported after boot.py
try:
    main()
except KeyboardInterrupt:
    print("\n\n=== Stopped by user (CTRL+C) ===")
    print("Device halted. Press CTRL+D to soft reset or reconnect.")
except Exception as e:
    print(f"\n!!! Fatal error: {e}")
    import sys
    sys.print_exception(e)
    print("\n=== System will restart in 10 seconds ===")
    print("Press CTRL+C to cancel restart")
    try:
        time.sleep(10)
        machine.reset()
    except KeyboardInterrupt:
        print("Restart cancelled")