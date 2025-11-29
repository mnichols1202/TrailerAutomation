"""
Pico 2W WiFi Hardware Reset and Initialization
This fixes the CYW43 chip initialization issues
"""

import network
import time
import machine

print("\n" + "="*60)
print("Pico 2W WiFi Hardware Reset")
print("="*60)

# Step 1: Complete hardware reset
print("\n[1] Performing complete WiFi hardware reset...")

try:
    # Get existing WLAN object if any
    wlan = network.WLAN(network.STA_IF)
    
    # Force complete shutdown
    print("    Shutting down WiFi...")
    wlan.active(False)
    time.sleep(2)
    
    # Delete the object to force cleanup
    del wlan
    time.sleep(1)
    
    print("    ✓ WiFi shut down")
    
except Exception as e:
    print(f"    Initial shutdown: {e}")

# Step 2: Create fresh WLAN instance
print("\n[2] Initializing fresh WiFi instance...")
wlan = network.WLAN(network.STA_IF)
time.sleep(1)

# Step 3: Activate with proper timing
print("\n[3] Activating WiFi hardware...")
wlan.active(True)
time.sleep(3)  # Longer wait for CYW43 chip to initialize

# Check if active
is_active = wlan.active()
print(f"    WLAN active: {is_active}")

if not is_active:
    print("\n    ✗ WiFi failed to activate!")
    print("    This usually requires a hard reset of the Pico")
    print("    Try: Unplug USB, wait 5 seconds, plug back in")
else:
    print("    ✓ WiFi hardware activated")
    
    # Step 4: Apply power management fix
    print("\n[4] Applying WiFi configuration...")
    
    try:
        # Disable power management
        wlan.config(pm=0xa11140)
        print("    ✓ Power management disabled")
    except Exception as e:
        print(f"    ! PM config: {e}")
    
    try:
        # Set country
        wlan.config(country='US')
        print("    ✓ Country: US")
    except Exception as e:
        print(f"    ! Country: {e}")
    
    # Step 5: Test scan
    print("\n[5] Testing WiFi scan...")
    
    try:
        print("    Scanning...")
        networks = wlan.scan()
        print(f"    ✓ Found {len(networks)} networks")
        
        if len(networks) > 0:
            print("\n    Top 5 networks:")
            for i, net in enumerate(networks[:5]):
                ssid = net[0].decode('utf-8')
                rssi = net[3]
                channel = net[2]
                print(f"      {i+1}. {ssid:25} {rssi:4}dBm  Ch:{channel:2}")
        
        print("\n" + "="*60)
        print("✓ WiFi hardware is working!")
        print("="*60)
        print("\nYou can now run: import wifi_optimized")
        
    except Exception as e:
        print(f"\n    ✗ Scan failed: {e}")
        print("\n    TROUBLESHOOTING:")
        print("    1. Unplug Pico USB cable")
        print("    2. Wait 10 seconds")
        print("    3. Plug back in")
        print("    4. Run this script again")
        print("    5. If still fails, press BOOTSEL and re-flash MicroPython")

print("\n" + "="*60)
