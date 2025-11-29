"""
boot.py - Runs on every boot before main.py
Performs initial hardware setup and configuration
"""

import gc
import machine

print("\n" + "="*60)
print("TrailerAutomationClientPico - Boot Sequence")
print("="*60)
print(f"Device ID: {machine.unique_id().hex()}")
print(f"Frequency: {machine.freq() / 1_000_000:.1f} MHz")

# Enable garbage collection
gc.enable()
print(f"Free memory: {gc.mem_free()} bytes")

# Initial garbage collection
gc.collect()
print("Garbage collection enabled")

print("="*60)
print("Boot complete. Starting main.py...\n")
