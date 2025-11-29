"""
TrailerAutomationClientPico - MicroPython 1.26 for Pico 2 W
Main entry point for the trailer automation client.
"""

import time
import json
import machine
import network
import urequests as requests
import _thread
from gateway_discovery import discover_gateway
from sht31_reader import Sht31Reader
from config import Config

# Load configuration
config = Config()

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
    """Connect to WiFi network."""
    wlan = network.WLAN(network.STA_IF)
    wlan.active(True)
    
    if wlan.isconnected():
        print("Already connected to WiFi")
        print(f"IP: {wlan.ifconfig()[0]}")
        return True
    
    print(f"Connecting to WiFi: {config.WIFI_SSID}")
    wlan.connect(config.WIFI_SSID, config.WIFI_PASSWORD)
    
    # Wait for connection
    max_wait = 20
    while max_wait > 0:
        if wlan.isconnected():
            break
        max_wait -= 1
        print("Waiting for connection...")
        blink_led(1, 50)
        time.sleep(1)
    
    if wlan.isconnected():
        ip = wlan.ifconfig()[0]
        print(f"Connected! IP: {ip}")
        blink_led(3, 100)
        return True
    else:
        print("Failed to connect to WiFi")
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

# Entry point
if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\nStopped by user")
    except Exception as e:
        print(f"Fatal error: {e}")
        import sys
        sys.print_exception(e)