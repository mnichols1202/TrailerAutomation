"""
Direct WiFi test with hardcoded credentials
This bypasses config.py to verify the credentials work
"""

import network
import time

SSID = "MM0001"
PASSWORD = "J0shua07"

print("\n" + "="*60)
print("Direct WiFi Test (hardcoded credentials)")
print("="*60)

print(f"\nSSID: '{SSID}'")
print(f"Password: '{PASSWORD}'")
print(f"Password length: {len(PASSWORD)}")

wlan = network.WLAN(network.STA_IF)
wlan.active(False)
time.sleep(0.5)
wlan.active(True)
time.sleep(2)

print(f"\nWLAN active: {wlan.active()}")

# Apply optimizations
try:
    wlan.config(pm=0xa11140)
    print("Power management: DISABLED")
except:
    pass

try:
    wlan.config(hostname='Pico2W-Test')
    print("Hostname: Pico2W-Test")
except:
    pass

print("\nConnecting...")
wlan.connect(SSID, PASSWORD)

# Wait for connection
max_wait = 30
status_names = {
    -3: "BAD_AUTH", -2: "TIMEOUT", -1: "GENERIC_FAIL",
    0: "IDLE", 1: "CONNECTING", 2: "WRONG_PASSWORD",
    3: "NO_AP_FOUND", 4: "CONNECT_FAIL", 5: "GOT_IP"
}

while max_wait > 0:
    status = wlan.status()
    status_str = status_names.get(status, f"UNKNOWN({status})")
    
    if max_wait % 5 == 0 or status != 1:
        print(f"  [{max_wait:2}s] {status_str}")
    
    if wlan.isconnected():
        print(f"\n✓ CONNECTED!")
        print(f"IP: {wlan.ifconfig()[0]}")
        break
    
    if status in [-3, 2, 3, 4]:
        print(f"\n✗ FAILED: {status_str}")
        print("\nIf this fails with WRONG_PASSWORD:")
        print("  1. Check router admin panel for correct password")
        print("  2. Try connecting from phone/laptop to verify password")
        print("  3. Check router security is WPA2-PSK (not WPA3)")
        break
    
    max_wait -= 1
    time.sleep(1)

print("="*60 + "\n")
