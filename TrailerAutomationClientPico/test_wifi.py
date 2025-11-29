"""
Test script for WiFi connectivity with Pico 2W optimizations
Run this independently to verify WiFi configuration.
"""

import network
import time
import config

def test_wifi():
    """Test WiFi connection with detailed debugging and optimizations."""
    print("=" * 60)
    print("WiFi Connection Test - Pico 2W Optimized")
    print("=" * 60)
    
    print(f"\n[Config]")
    print(f"  SSID: '{config.WIFI_SSID}'")
    print(f"  Password length: {len(config.WIFI_PASSWORD)} characters")
    
    # Note: rp2.country() breaks WiFi scanning on this Pico 2W build - do not use!
    
    # Proper WiFi initialization for Pico 2W
    print(f"\n[1] Initializing WiFi hardware...")
    wlan = network.WLAN(network.STA_IF)
    
    # Clean state - deactivate first
    wlan.active(False)
    time.sleep(0.5)
    
    # Activate with proper timing
    wlan.active(True)
    time.sleep(2)  # Give CYW43 chip time to initialize
    
    print(f"  WLAN activated: {wlan.active()}")
    
    # Apply critical WiFi optimizations
    print(f"\n[2] Applying WiFi optimizations...")
    try:
        # CRITICAL: Disable power management (fixes connection issues)
        wlan.config(pm=0xa11140)
        print(f"  ✓ Power management: DISABLED")
    except Exception as e:
        print(f"  ! Power management: {e}")
    
    # Note: country config not supported in this MicroPython build
    
    try:
        wlan.config(txpower=20)
        print(f"  ✓ TX Power: 20 dBm")
    except Exception as e:
        print(f"  ! TX Power: {e}")
    
    try:
        wlan.config(hostname='Pico2W-Test')
        print(f"  ✓ Hostname: Pico2W-Test")
    except Exception as e:
        print(f"  ! Hostname: {e}")
    
    if wlan.isconnected():
        print(f"\n[3] Already connected!")
        print_connection_info(wlan)
        return True
    
    # Scan for networks
    print(f"\n[3] Scanning for WiFi networks...")
    try:
        networks = wlan.scan()
        print(f"  Found {len(networks)} networks:\n")
        target_found = False
        target_rssi = None
        
        for net in networks:
            ssid = net[0].decode('utf-8')
            rssi = net[3]
            channel = net[2]
            security = net[4]
            
            is_target = (ssid == config.WIFI_SSID)
            if is_target:
                target_found = True
                target_rssi = rssi
            
            marker = " <<< TARGET" if is_target else ""
            prefix = "  >>>" if is_target else "     "
            print(f"{prefix} {ssid:25} {rssi:4}dBm  Ch:{channel:2}  Sec:{security}{marker}")
        
        print()
        if not target_found:
            print("  " + "!" * 56)
            print(f"  ERROR: Target SSID '{config.WIFI_SSID}' NOT FOUND!")
            print("  " + "!" * 56)
            print("  Possible causes:")
            print("    • SSID spelling is wrong (check capitalization)")
            print("    • Router is broadcasting on 5GHz (Pico only supports 2.4GHz)")
            print("    • Router is out of range")
            print("    • Router SSID is hidden")
            return False
        else:
            print(f"  ✓ Target network found!")
            if target_rssi and target_rssi < -75:
                print(f"    ⚠ Weak signal: {target_rssi}dBm (move closer to router)")
            
    except Exception as e:
        print(f"  ✗ Network scan error: {e}")
        print(f"  This usually means WiFi chip needs reset")
        print(f"  Try: Unplug USB, wait 10s, plug back in")
        return False
    
    print(f"\n[4] Connecting to '{config.WIFI_SSID}'...")
    try:
        wlan.connect(config.WIFI_SSID, config.WIFI_PASSWORD)
    except Exception as e:
        print(f"  ✗ Connection initiation failed: {e}")
        return False
    
    # Wait for connection with detailed status
    max_wait = 30
    print(f"  Monitoring connection (30s timeout):\n")
    
    status_names = {
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
    
    last_status = None
    while max_wait > 0:
        status = wlan.status()
        status_str = status_names.get(status, f"UNKNOWN({status})")
        
        # Only print on status change or every 5 seconds
        if status != last_status or max_wait % 5 == 0:
            print(f"    [{max_wait:2}s] {status_str}")
            last_status = status
        
        if wlan.isconnected():
            print(f"    [{max_wait:2}s] {status_str} - SUCCESS!\n")
            break
        
        # Check for fatal errors
        if status == -3:
            print(f"\n    {'!' * 52}")
            print(f"    ERROR: BAD AUTH (Status -3)")
            print(f"    {'!' * 52}")
            print(f"    Authentication failed - usually means:")
            print(f"      • Wrong password (check every character!)")
            print(f"      • Router security incompatibility")
            print(f"      • Router is WPA3 only (Pico needs WPA2/WPA2-PSK)")
            print(f"\n    Current password: '{config.WIFI_PASSWORD}'")
            print(f"    Password length: {len(config.WIFI_PASSWORD)} characters")
            print(f"\n    Try:")
            print(f"      • Verify password character by character")
            print(f"      • Check router is WPA2-PSK (not WPA3 only)")
            print(f"      • Create guest network with simple password to test")
            return False
        elif status == 2:
            print(f"\n    {'!' * 52}")
            print(f"    ERROR: WRONG PASSWORD")
            print(f"    {'!' * 52}")
            print(f"    Check config.WIFI_PASSWORD in config.py")
            return False
        elif status == 3:
            print(f"\n    {'!' * 52}")
            print(f"    ERROR: NO AP FOUND")
            print(f"    {'!' * 52}")
            print(f"    Network disappeared or SSID is wrong")
            return False
        elif status == 4:
            print(f"\n    {'!' * 52}")
            print(f"    ERROR: CONNECTION FAILED")
            print(f"    {'!' * 52}")
            print(f"    Router rejected connection")
            print(f"    Check: MAC filtering, max clients, router logs")
            return False
        
        max_wait -= 1
        time.sleep(1)
    
    if wlan.isconnected():
        print("  " + "="*56)
        print("  ✓✓✓ CONNECTION SUCCESSFUL! ✓✓✓")
        print("  " + "="*56)
        print_connection_info(wlan)
        return True
    else:
        status = wlan.status()
        print("\n  " + "="*56)
        print("  CONNECTION TIMEOUT")
        print("  " + "="*56)
        print(f"  Final status: {status_names.get(status, f'UNKNOWN({status})')}")
        print(f"\n  Troubleshooting:")
        print(f"    1. Verify SSID and password in config.py")
        print(f"    2. Ensure network is 2.4GHz (not 5GHz)")
        print(f"    3. Move Pico closer to router")
        print(f"    4. Check router security is WPA2-PSK")
        print(f"    5. Try restarting router")
        print(f"    6. Unplug Pico USB, wait 10s, reconnect")
        return False

def print_connection_info(wlan):
    """Print detailed connection information."""
    ifconfig = wlan.ifconfig()
    print(f"  IP Address:  {ifconfig[0]}")
    print(f"  Subnet Mask: {ifconfig[1]}")
    print(f"  Gateway:     {ifconfig[2]}")
    print(f"  DNS Server:  {ifconfig[3]}")
    try:
        mac = wlan.config('mac')
        mac_str = ':'.join(['%02x' % b for b in mac])
        print(f"  MAC Address: {mac_str}")
    except:
        pass
    try:
        rssi = wlan.status('rssi')
        print(f"  Signal:      {rssi} dBm")
    except:
        pass

# Auto-run when imported
print()
result = test_wifi()
print("\n" + "="*60)
if result:
    print("✓ WiFi test PASSED")
else:
    print("✗ WiFi test FAILED")
print("="*60 + "\n")
