"""
Minimal boot test - Upload this as main.py to verify boot sequence works
"""

print("\n" + "="*60)
print("BOOT TEST - If you see this, boot sequence is working!")
print("="*60)

import machine
import time

print(f"Device ID: {machine.unique_id().hex()}")
print(f"Frequency: {machine.freq() / 1_000_000:.1f} MHz")

# Blink onboard LED to show it's alive
try:
    from machine import Pin
    led = Pin("LED", Pin.OUT)
    for i in range(5):
        led.toggle()
        print(f"Blink {i+1}/5")
        time.sleep(0.5)
    print("\nBoot test complete! Hardware is responding.")
except Exception as e:
    print(f"LED test failed: {e}")

print("\nYou should have seen:")
print("  1. This text output")
print("  2. 5 LED blinks")
print("\nIf you see this, the Pico is working correctly.")
print("="*60)
