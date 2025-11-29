"""
I2C Scanner Utility
Scans I2C bus and displays all detected devices.
Useful for troubleshooting I2C connections.
"""

import machine
import time

def scan_i2c(i2c_id=0, sda_pin=0, scl_pin=1):
    """
    Scan I2C bus for connected devices.
    
    Args:
        i2c_id: I2C bus number (0 or 1)
        sda_pin: GPIO pin for SDA
        scl_pin: GPIO pin for SCL
    """
    print("=" * 50)
    print("I2C Scanner Utility")
    print("=" * 50)
    print(f"I2C Bus: {i2c_id}")
    print(f"SDA Pin: GP{sda_pin} (Pin {sda_pin + 1})")
    print(f"SCL Pin: GP{scl_pin} (Pin {scl_pin + 1})")
    print()
    
    try:
        # Initialize I2C
        i2c = machine.I2C(
            i2c_id,
            sda=machine.Pin(sda_pin),
            scl=machine.Pin(scl_pin),
            freq=100000
        )
        
        print("Scanning I2C bus...")
        time.sleep_ms(100)
        
        devices = i2c.scan()
        
        print()
        print("=" * 50)
        
        if devices:
            print(f"Found {len(devices)} device(s):")
            print("-" * 50)
            
            for addr in devices:
                device_name = get_device_name(addr)
                print(f"  Address: 0x{addr:02X} ({addr:3d}) - {device_name}")
            
            print("-" * 50)
            
            # Check for common devices
            if 0x44 in devices or 0x45 in devices:
                print("\n✓ SHT31/SHT3x detected!")
            if 0x76 in devices or 0x77 in devices:
                print("\n✓ BME280/BMP280 detected!")
            if 0x48 in devices:
                print("\n✓ ADS1115 or TMP102 detected!")
            
        else:
            print("No I2C devices found!")
            print()
            print("Troubleshooting:")
            print("  1. Check wiring connections")
            print("  2. Verify power (3.3V) to sensor")
            print("  3. Check SDA and SCL are not swapped")
            print("  4. Try different I2C bus or pins")
            print("  5. Check if pull-up resistors are needed")
        
        print("=" * 50)
        
    except Exception as e:
        print(f"Error: {e}")
        import sys
        sys.print_exception(e)

def get_device_name(addr):
    """Return likely device name for common I2C addresses."""
    devices = {
        0x08: "Reserved",
        0x0E: "MLX90614 (IR Temperature)",
        0x1E: "HMC5883L (Magnetometer)",
        0x23: "BH1750 (Light Sensor)",
        0x27: "LCD Display",
        0x29: "VL53L0X (ToF Distance) / TSL2561",
        0x38: "VEML7700 / FT6206",
        0x39: "TSL2561 / APDS-9960",
        0x3C: "OLED Display (128x64)",
        0x3D: "OLED Display (128x64)",
        0x40: "PCA9685 (PWM Driver) / Si7021",
        0x44: "SHT31 (Temp/Humidity)",
        0x45: "SHT31 (Temp/Humidity - Alt)",
        0x48: "ADS1115 (ADC) / TMP102",
        0x49: "ADS1115 / AS7262",
        0x4A: "ADS1115",
        0x4B: "ADS1115",
        0x50: "EEPROM (24C32)",
        0x51: "EEPROM / RTC",
        0x52: "EEPROM",
        0x53: "ADXL345 (Accelerometer)",
        0x57: "EEPROM (24C32)",
        0x5A: "MLX90614 / CCS811",
        0x5B: "CCS811 (CO2)",
        0x68: "DS1307 / DS3231 (RTC) / MPU6050",
        0x69: "MPU6050 (Gyro/Accel)",
        0x76: "BME280 / BMP280 (Temp/Pressure)",
        0x77: "BME280 / BMP280 (Alt) / BMA180",
    }
    
    return devices.get(addr, "Unknown Device")

def scan_all_buses():
    """Scan both I2C buses with common pin configurations."""
    print("\n" + "=" * 50)
    print("Scanning all common I2C configurations...")
    print("=" * 50 + "\n")
    
    configs = [
        # (bus, sda, scl, description)
        (0, 0, 1, "I2C0: GP0 (SDA), GP1 (SCL) - Default"),
        (0, 4, 5, "I2C0: GP4 (SDA), GP5 (SCL)"),
        (0, 8, 9, "I2C0: GP8 (SDA), GP9 (SCL)"),
        (1, 2, 3, "I2C1: GP2 (SDA), GP3 (SCL)"),
        (1, 6, 7, "I2C1: GP6 (SDA), GP7 (SCL)"),
        (1, 10, 11, "I2C1: GP10 (SDA), GP11 (SCL)"),
    ]
    
    for bus, sda, scl, desc in configs:
        print(f"\n{desc}")
        print("-" * 50)
        try:
            i2c = machine.I2C(
                bus,
                sda=machine.Pin(sda),
                scl=machine.Pin(scl),
                freq=100000
            )
            devices = i2c.scan()
            if devices:
                for addr in devices:
                    print(f"  Found: 0x{addr:02X} - {get_device_name(addr)}")
            else:
                print("  No devices found")
        except Exception as e:
            print(f"  Error: {e}")
        
        time.sleep_ms(100)

# Auto-run when imported
print("\n")
scan_i2c(i2c_id=0, sda_pin=0, scl_pin=1)
print("\nTo scan all I2C buses, run: scan_all_buses()")
