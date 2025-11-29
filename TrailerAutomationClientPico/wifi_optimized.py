"""
Pico 2W WiFi Connection Script with ALL Optimizations
This includes all the quirks and fixes for the finicky Pico W WiFi
"""

import network
import time

# WiFi credentials
SSID = "MM0001"
PASSWORD = "S3cur1ty3742"

print("\n" + "="*60)
print("Pico 2W WiFi Connection - OPTIMIZED VERSION")
print("="*60)

# Step 1: Initialize WLAN interface with clean state
print("\n[1] Initializing WiFi...")
wlan = network.WLAN(network.STA_IF)

# Force clean state by deactivating first
wlan.active(False)
time.sleep(0.5)

wlan.active(True)
time.sleep(1)
print(f"    WLAN interface active: {wlan.active()}")

# Step 2: Apply critical WiFi configuration tweaks
print("\n[2] Applying Pico W WiFi optimizations...")

try:
    # CRITICAL: Disable power management (0xa11140 = PM_NONE)
    # This is the #1 fix for Pico W connection issues!
    wlan.config(pm=0xa11140)
    print("    ✓ Power management DISABLED")
except Exception as e:
    print(f"    ✗ Power management: {e}")

try:
    # Set regulatory domain (country code)
    wlan.config(country='US')  # Change to: US, GB, DE, FR, JP, etc.
    print("    ✓ Country code: US")
except Exception as e:
    print(f"    ! Country code (not critical): {e}")

try:
    # Set maximum TX power for better range
    wlan.config(txpower=20)  # 20 dBm = max power
    print("    ✓ TX Power: 20 dBm")
except Exception as e:
    print(f"    ! TX power: {e}")

try:
    # Set hostname
    wlan.config(hostname='Pico2W-Trailer')
    print("    ✓ Hostname: Pico2W-Trailer")
except Exception as e:
    print(f"    ! Hostname: {e}")

try:
    # Disable reconnect attempts (we'll handle manually)
    wlan.config(reconnects=0)
    print("    ✓ Auto-reconnect: disabled (manual control)")
except Exception as e:
    print(f"    ! Reconnects: {e}")

# Step 3: Check if already connected
if wlan.isconnected():
    print("\n[3] Already connected!")
    ifconfig = wlan.ifconfig()
    mac = wlan.config('mac')
    mac_str = ':'.join(['%02x' % b for b in mac])
    print(f"    IP:      {ifconfig[0]}")
    print(f"    Gateway: {ifconfig[2]}")
    print(f"    MAC:     {mac_str}")
else:
    # Step 4: Scan for networks
    print(f"\n[3] Scanning for WiFi networks...")
    
    try:
        networks = wlan.scan()
        print(f"    Found {len(networks)} networks:\n")
        target_found = False
        target_rssi = None
        target_channel = None
        
        for net in networks:
            ssid = net[0].decode('utf-8')
            rssi = net[3]
            channel = net[2]
            security = net[4]
            
            if ssid == SSID:
                target_found = True
                target_rssi = rssi
                target_channel = channel
                print(f"    >>> {ssid:25} RSSI: {rssi:4}dBm  Ch: {channel:2}  Sec: {security}  <<< TARGET")
            else:
                print(f"        {ssid:25} RSSI: {rssi:4}dBm  Ch: {channel:2}  Sec: {security}")
        
        print()
        if not target_found:
            print("    " + "!"*56)
            print(f"    ERROR: Network '{SSID}' NOT FOUND!")
            print("    " + "!"*56)
            print("    Possible causes:")
            print("      • SSID spelling is wrong")
            print("      • Network is 5GHz only (Pico only supports 2.4GHz)")
            print("      • Router is out of range")
            print("      • SSID is hidden")
            print("\n" + "="*60)
            raise SystemExit
        else:
            print(f"    ✓ Target network found!")
            print(f"      Signal: {target_rssi}dBm  Channel: {target_channel}")
            if target_rssi < -75:
                print(f"      ⚠ WARNING: Weak signal ({target_rssi}dBm)")
                print("         Consider moving closer to router")
            
    except SystemExit:
        raise
    except Exception as e:
        print(f"    Network scan error: {e}")
    
    # Step 5: Disconnect if somehow connected
    print(f"\n[4] Preparing to connect...")
    if wlan.isconnected():
        print("    Disconnecting from previous network...")
        wlan.disconnect()
        time.sleep(1)
    
    # Step 6: Connect with retries
    print(f"\n[5] Connecting to '{SSID}'...")
    print(f"    Password: {'*' * len(PASSWORD)} ({len(PASSWORD)} chars)")
    
    max_attempts = 3
    attempt = 1
    
    while attempt <= max_attempts:
        if attempt > 1:
            print(f"\n    --- Retry attempt {attempt}/{max_attempts} ---")
            wlan.active(False)
            time.sleep(1)
            wlan.active(True)
            time.sleep(1)
            # Reapply power management fix
            try:
                wlan.config(pm=0xa11140)
            except:
                pass
        
        print(f"    Initiating connection...")
        try:
            wlan.connect(SSID, PASSWORD)
        except Exception as e:
            print(f"    Connection initiation failed: {e}")
            attempt += 1
            continue
        
        # Monitor connection status
        max_wait = 30
        print(f"    Monitoring status (30s timeout):\n")
        
        last_status = None
        while max_wait > 0:
            status = wlan.status()
            
            # Map status codes with negative values
            status_map = {
                -3: "BAD_AUTH",
                -2: "TIMEOUT",
                -1: "GENERIC_FAIL",
                0: "IDLE",
                1: "CONNECTING",
                2: "WRONG_PASSWORD",
                3: "NO_AP_FOUND",
                4: "CONNECT_FAIL",
                5: "GOT_IP"
            }
            status_msg = status_map.get(status, f"UNKNOWN({status})")
            
            # Only print on status change or every 5 seconds
            if status != last_status or max_wait % 5 == 0:
                print(f"      [{max_wait:2}s] {status_msg}")
                last_status = status
            
            # Check for success
            if wlan.isconnected():
                print(f"\n    ✓✓✓ CONNECTED SUCCESSFULLY! ✓✓✓\n")
                ifconfig = wlan.ifconfig()
                mac = wlan.config('mac')
                mac_str = ':'.join(['%02x' % b for b in mac])
                print(f"    IP Address:  {ifconfig[0]}")
                print(f"    Netmask:     {ifconfig[1]}")
                print(f"    Gateway:     {ifconfig[2]}")
                print(f"    DNS Server:  {ifconfig[3]}")
                print(f"    MAC Address: {mac_str}")
                print(f"    RSSI:        {wlan.status('rssi')} dBm")
                break
            
            # Check for fatal errors
            if status == -3:
                print(f"\n    ✗✗✗ ERROR: BAD AUTHENTICATION (Status -3) ✗✗✗")
                print("    This usually means:")
                print("      • Wrong password (check every character!)")
                print("      • Router security type incompatible")
                print("      • Router is WPA3-only (Pico needs WPA2/WPA2-PSK)")
                print(f"\n    Current password: '{PASSWORD}'")
                break
            elif status == 2:
                print(f"\n    ✗✗✗ ERROR: WRONG PASSWORD ✗✗✗")
                break
            elif status == 3:
                print(f"\n    ✗✗✗ ERROR: NO AP FOUND ✗✗✗")
                print("    Network disappeared during connection")
                break
            elif status == 4:
                print(f"\n    ✗✗✗ ERROR: CONNECTION FAILED ✗✗✗")
                print("    Router rejected the connection")
                print("    Check: MAC filtering, max clients, router logs")
                break
            
            max_wait -= 1
            time.sleep(1)
        
        # Check if connected
        if wlan.isconnected():
            break
        else:
            print(f"\n    Connection attempt {attempt} failed")
            attempt += 1
            if attempt <= max_attempts:
                print("    Will retry...")
                time.sleep(2)
    
    if not wlan.isconnected():
        print("\n" + "!"*60)
        print("    ALL CONNECTION ATTEMPTS FAILED")
        print("!"*60)
        status = wlan.status()
        status_map = {-3: "BAD_AUTH", -2: "TIMEOUT", -1: "GENERIC_FAIL",
                      0: "IDLE", 1: "CONNECTING", 2: "WRONG_PASSWORD",
                      3: "NO_AP_FOUND", 4: "CONNECT_FAIL", 5: "GOT_IP"}
        print(f"    Final status: {status_map.get(status, f'UNKNOWN({status})')}")
        print("\n    Troubleshooting steps:")
        print("      1. Verify router is 2.4GHz (not 5GHz only)")
        print("      2. Check router security is WPA2-PSK (not WPA3 only)")
        print("      3. Verify password character by character")
        print("      4. Try creating a guest network with simple password")
        print("      5. Check router logs for connection attempts")
        print("      6. Try disabling MAC filtering on router")
        print("      7. Reboot the router")

print("\n" + "="*60)
