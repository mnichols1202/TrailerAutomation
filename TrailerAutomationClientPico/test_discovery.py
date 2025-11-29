"""
Test script for mDNS gateway discovery
Run this independently to verify gateway discovery is working.
"""

import network
import time
from config import Config
from gateway_discovery import discover_gateway

def test_discovery():
    """Test mDNS gateway discovery."""
    print("=" * 50)
    print("Gateway Discovery Test")
    print("=" * 50)
    
    config = Config()
    
    # Ensure WiFi is connected
    wlan = network.WLAN(network.STA_IF)
    wlan.active(True)
    
    if not wlan.isconnected():
        print("Connecting to WiFi first...")
        wlan.connect(config.WIFI_SSID, config.WIFI_PASSWORD)
        
        max_wait = 20
        while max_wait > 0 and not wlan.isconnected():
            max_wait -= 1
            time.sleep(1)
        
        if not wlan.isconnected():
            print("ERROR: Cannot connect to WiFi")
            return
    
    print(f"Connected to WiFi")
    print(f"Local IP: {wlan.ifconfig()[0]}")
    print()
    
    # Perform discovery
    print("Starting mDNS discovery...")
    print(f"Service: {config.MDNS_SERVICE_TYPE}")
    print(f"Timeout: {config.MDNS_DISCOVERY_TIMEOUT_SEC}s")
    print()
    
    gateway_url = discover_gateway(
        service_type=config.MDNS_SERVICE_TYPE,
        timeout_sec=config.MDNS_DISCOVERY_TIMEOUT_SEC
    )
    
    print()
    print("=" * 50)
    
    if gateway_url:
        print("DISCOVERY SUCCESSFUL!")
        print("=" * 50)
        print(f"Gateway URL: {gateway_url}")
        print()
        print("Next step: Test HTTP connection")
        
        # Try a simple HTTP request
        try:
            import urequests as requests
            print(f"Testing HTTP GET to {gateway_url}...")
            response = requests.get(gateway_url, timeout=5)
            print(f"Response code: {response.status_code}")
            print(f"Response: {response.text[:100]}")
            response.close()
        except Exception as e:
            print(f"HTTP test failed: {e}")
    else:
        print("DISCOVERY FAILED!")
        print("=" * 50)
        print("Please check:")
        print("  - TrailerAutomationGateway is running")
        print("  - Gateway is on the same network")
        print("  - Gateway has mDNS enabled")
        print("  - Firewall is not blocking mDNS (port 5353)")

# Auto-run when imported
print("\n")
test_discovery()
