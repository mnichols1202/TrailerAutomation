"""
Simple WiFi debug script - minimal dependencies
Copy and paste this directly into REPL or upload and run
"""

import network
import time

# Your WiFi credentials
SSID = "TP-Link_IoT_2F56"
PASSWORD = "00393966"

print("\n" + "="*50)
print("WiFi Debug Script")
print("="*50)

wlan = network.WLAN(network.STA_IF)
print(f"1. WLAN active: {wlan.active()}")

wlan.active(True)
time.sleep(1)
print(f"2. WLAN activated: {wlan.active()}")

if wlan.isconnected():
    print("\n3. Already connected!")
    ifconfig = wlan.ifconfig()
    print(f"   IP: {ifconfig[0]}")
    print(f"   Gateway: {ifconfig[2]}")
else:
    print("\n3. Not connected, scanning networks...")
    
    try:
        networks = wlan.scan()
        print(f"\n4. Found {len(networks)} networks:")
        target_found = False
        
        for net in networks:
            ssid = net[0].decode('utf-8')
            rssi = net[3]
            channel = net[2]
            
            if ssid == SSID:
                target_found = True
                print(f"   >>> {ssid} (RSSI: {rssi}dBm, Ch: {channel}) <<< YOUR NETWORK")
            else:
                print(f"       {ssid} (RSSI: {rssi}dBm, Ch: {channel})")
        
        if not target_found:
            print(f"\n   !!! WARNING: '{SSID}' NOT FOUND !!!")
            print("   - Check SSID spelling")
            print("   - Network might be 5GHz (Pico only supports 2.4GHz)")
        
    except Exception as e:
        print(f"   Scan error: {e}")
    
    print(f"\n5. Connecting to '{SSID}'...")
    wlan.connect(SSID, PASSWORD)
    
    print("6. Waiting for connection...")
    max_wait = 20
    
    while max_wait > 0:
        status = wlan.status()
        
        # Status codes
        if status == 0:
            status_msg = "IDLE"
        elif status == 1:
            status_msg = "CONNECTING"
        elif status == 2:
            status_msg = "WRONG_PASSWORD"
        elif status == 3:
            status_msg = "NO_AP_FOUND"
        elif status == 4:
            status_msg = "CONNECT_FAIL"
        elif status == 5:
            status_msg = "GOT_IP"
        else:
            status_msg = f"UNKNOWN({status})"
        
        print(f"   [{max_wait:2}s] Status: {status_msg}")
        
        if wlan.isconnected():
            print("\n7. CONNECTED!")
            ifconfig = wlan.ifconfig()
            print(f"   IP:      {ifconfig[0]}")
            print(f"   Netmask: {ifconfig[1]}")
            print(f"   Gateway: {ifconfig[2]}")
            print(f"   DNS:     {ifconfig[3]}")
            break
        
        if status == 2:
            print("\n   ERROR: Wrong password!")
            break
        elif status == 3:
            print("\n   ERROR: Network not found!")
            break
        elif status == 4:
            print("\n   ERROR: Connection failed!")
            break
        
        max_wait -= 1
        time.sleep(1)
    
    if not wlan.isconnected():
        print("\n7. CONNECTION FAILED")
        print(f"   Final status: {status_msg}")

print("\n" + "="*50)
