"""
Test script for WiFi connectivity
Run this independently to verify WiFi configuration.
"""

import network
import time
from config import Config

def test_wifi():
    """Test WiFi connection."""
    print("=" * 50)
    print("WiFi Connection Test")
    print("=" * 50)
    
    config = Config()
    
    print(f"SSID: {config.WIFI_SSID}")
    print()
    
    wlan = network.WLAN(network.STA_IF)
    wlan.active(True)
    
    if wlan.isconnected():
        print("Already connected!")
        print_connection_info(wlan)
        return
    
    print("Connecting to WiFi...")
    wlan.connect(config.WIFI_SSID, config.WIFI_PASSWORD)
    
    # Wait for connection
    max_wait = 30
    while max_wait > 0:
        if wlan.isconnected():
            break
        max_wait -= 1
        print(f"Waiting... ({max_wait}s remaining)")
        time.sleep(1)
    
    if wlan.isconnected():
        print("\n" + "=" * 50)
        print("CONNECTION SUCCESSFUL!")
        print("=" * 50)
        print_connection_info(wlan)
    else:
        print("\n" + "=" * 50)
        print("CONNECTION FAILED!")
        print("=" * 50)
        print("Please check:")
        print("  - SSID is correct")
        print("  - Password is correct")
        print("  - Network is 2.4GHz (Pico W doesn't support 5GHz)")
        print("  - Pico is within WiFi range")

def print_connection_info(wlan):
    """Print detailed connection information."""
    ifconfig = wlan.ifconfig()
    print(f"IP Address:  {ifconfig[0]}")
    print(f"Subnet Mask: {ifconfig[1]}")
    print(f"Gateway:     {ifconfig[2]}")
    print(f"DNS Server:  {ifconfig[3]}")
    print(f"MAC Address: {':'.join('%02x' % b for b in wlan.config('mac'))}")
    print()

if __name__ == "__main__":
    test_wifi()
