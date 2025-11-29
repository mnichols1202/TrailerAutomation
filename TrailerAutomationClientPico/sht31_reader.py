"""
SHT31 Temperature and Humidity Sensor Reader for MicroPython
Compatible with MicroPython 1.26 on Pico 2 W
"""

import machine
import time

class Sht31Reader:
    """
    Driver for SHT3x (SHT31) temperature and humidity sensor.
    
    The SHT31 is connected via I2C. Default address is 0x44.
    """
    
    # SHT31 Commands
    CMD_MEASURE_HIGH_REP = 0x2400  # High repeatability measurement
    CMD_MEASURE_MEDIUM_REP = 0x240B  # Medium repeatability measurement
    CMD_MEASURE_LOW_REP = 0x2416  # Low repeatability measurement
    CMD_SOFT_RESET = 0x30A2  # Soft reset
    CMD_HEATER_ENABLE = 0x306D  # Enable heater
    CMD_HEATER_DISABLE = 0x3066  # Disable heater
    CMD_STATUS = 0xF32D  # Read status register
    
    def __init__(self, i2c_id=0, sda_pin=0, scl_pin=1, address=0x44, freq=100000):
        """
        Initialize the SHT31 sensor.
        
        Args:
            i2c_id: I2C bus number (0 or 1)
            sda_pin: GPIO pin number for SDA
            scl_pin: GPIO pin number for SCL
            address: I2C address (0x44 or 0x45)
            freq: I2C frequency in Hz (default: 100kHz)
        """
        self.address = address
        
        # Initialize I2C
        self.i2c = machine.I2C(
            i2c_id,
            sda=machine.Pin(sda_pin),
            scl=machine.Pin(scl_pin),
            freq=freq
        )
        
        # Check if device is present
        devices = self.i2c.scan()
        if self.address not in devices:
            raise RuntimeError(f"SHT31 not found at address 0x{self.address:02X}. "
                             f"Found devices: {[hex(d) for d in devices]}")
        
        print(f"SHT31 found at address 0x{self.address:02X}")
        
        # Perform soft reset
        self._soft_reset()
        time.sleep_ms(10)
    
    def _write_command(self, cmd):
        """Write a 16-bit command to the sensor."""
        buf = bytearray(2)
        buf[0] = (cmd >> 8) & 0xFF
        buf[1] = cmd & 0xFF
        self.i2c.writeto(self.address, buf)
    
    def _read_data(self, num_bytes):
        """Read data from the sensor."""
        return self.i2c.readfrom(self.address, num_bytes)
    
    def _soft_reset(self):
        """Perform a soft reset of the sensor."""
        self._write_command(self.CMD_SOFT_RESET)
    
    def _crc8(self, data):
        """
        Calculate CRC-8 checksum for SHT31 data validation.
        Polynomial: 0x31 (x^8 + x^5 + x^4 + 1)
        """
        crc = 0xFF
        for byte in data:
            crc ^= byte
            for _ in range(8):
                if crc & 0x80:
                    crc = (crc << 1) ^ 0x31
                else:
                    crc = crc << 1
        return crc & 0xFF
    
    def read_measurement(self):
        """
        Read temperature and humidity from the sensor.
        
        Returns:
            tuple: (temperature_celsius, humidity_percent)
        """
        # Send measurement command (high repeatability, clock stretching disabled)
        self._write_command(self.CMD_MEASURE_HIGH_REP)
        
        # Wait for measurement to complete (max 15ms for high repeatability)
        time.sleep_ms(16)
        
        # Read 6 bytes: temp_msb, temp_lsb, temp_crc, humid_msb, humid_lsb, humid_crc
        data = self._read_data(6)
        
        # Verify CRC for temperature
        if self._crc8(data[0:2]) != data[2]:
            raise RuntimeError("Temperature CRC mismatch")
        
        # Verify CRC for humidity
        if self._crc8(data[3:5]) != data[5]:
            raise RuntimeError("Humidity CRC mismatch")
        
        # Convert temperature
        temp_raw = (data[0] << 8) | data[1]
        temperature_c = -45.0 + (175.0 * temp_raw / 65535.0)
        
        # Convert humidity
        humid_raw = (data[3] << 8) | data[4]
        humidity_percent = 100.0 * humid_raw / 65535.0
        
        return temperature_c, humidity_percent
    
    def read_temperature_c(self):
        """
        Read only the temperature in Celsius.
        
        Returns:
            float: Temperature in degrees Celsius
        """
        temp, _ = self.read_measurement()
        return temp
    
    def read_humidity_percent(self):
        """
        Read only the relative humidity.
        
        Returns:
            float: Relative humidity in percent (0-100)
        """
        _, humidity = self.read_measurement()
        return humidity
    
    def enable_heater(self):
        """Enable the internal heater (for testing or condensation removal)."""
        self._write_command(self.CMD_HEATER_ENABLE)
    
    def disable_heater(self):
        """Disable the internal heater."""
        self._write_command(self.CMD_HEATER_DISABLE)
    
    def read_status(self):
        """
        Read the status register.
        
        Returns:
            int: 16-bit status value
        """
        self._write_command(self.CMD_STATUS)
        time.sleep_ms(1)
        data = self._read_data(3)
        
        # Verify CRC
        if self._crc8(data[0:2]) != data[2]:
            raise RuntimeError("Status CRC mismatch")
        
        status = (data[0] << 8) | data[1]
        return status
