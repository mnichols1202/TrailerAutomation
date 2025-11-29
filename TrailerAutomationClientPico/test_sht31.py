"""
Test script for SHT31 sensor
Run this independently to verify your SHT31 sensor is working correctly.
"""

import time
from sht31_reader import Sht31Reader

def test_sht31():
    """Test the SHT31 sensor connection and readings."""
    print("=" * 50)
    print("SHT31 Sensor Test")
    print("=" * 50)
    
    try:
        # Initialize sensor with your configuration
        # Adjust i2c_id, sda_pin, scl_pin, address as needed
        print("Initializing SHT31...")
        sensor = Sht31Reader(
            i2c_id=0,
            sda_pin=0,  # GP0
            scl_pin=1,  # GP1
            address=0x44
        )
        
        print("Sensor initialized successfully!")
        print()
        
        # Read status
        print("Reading status register...")
        status = sensor.read_status()
        print(f"Status: 0x{status:04X}")
        print()
        
        # Take 10 readings
        print("Taking 10 readings (1 per second):")
        print("-" * 50)
        
        for i in range(10):
            temp_c, humidity = sensor.read_measurement()
            temp_f = (temp_c * 9/5) + 32
            
            print(f"Reading {i+1:2d}: "
                  f"Temp: {temp_c:5.2f}°C ({temp_f:5.2f}°F) | "
                  f"Humidity: {humidity:5.2f}%")
            
            time.sleep(1)
        
        print("-" * 50)
        print("Test completed successfully!")
        
    except Exception as e:
        print(f"Error: {e}")
        import sys
        sys.print_exception(e)

# Auto-run when imported
print("\n")
test_sht31()
