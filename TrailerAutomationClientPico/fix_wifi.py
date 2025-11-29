"""
Fix WiFi hardware initialization on Pico 2 W
This script properly initializes the CYW43 WiFi chip
"""

import time
import machine

print("\n" + "="*60)
print("WiFi Hardware Reset & Initialization")
print("="*60)

# Step 1: Hard reset the board
print("\n1. Performing hard reset...")
print("   (Board will restart)")
time.sleep(1)
machine.reset()
