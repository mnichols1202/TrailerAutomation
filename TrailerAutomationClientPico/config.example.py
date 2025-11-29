# Example configuration for testing
# Copy this to config.py and modify with your settings

class Config:
    """Configuration class for the Pico client."""
    
    # WiFi Settings - REQUIRED
    WIFI_SSID = "MyHomeNetwork"
    WIFI_PASSWORD = "MySecurePassword123"
    
    # Device Identity - Optional
    CLIENT_ID = None  # Auto-generated from Pico's unique ID
    DEVICE_TYPE = "Pico2W"
    FRIENDLY_NAME = "Pico2W-Garage"  # Give it a meaningful name!
    
    # Timing Intervals (in seconds) - Adjust as needed
    HEARTBEAT_INTERVAL_SEC = 10  # Gateway will mark offline if no heartbeat
    SENSOR_REPORT_INTERVAL_SEC = 30  # How often to sample temperature
    MDNS_DISCOVERY_TIMEOUT_SEC = 8  # Gateway discovery timeout
    
    # I2C Settings for SHT31 Sensor - Adjust for your wiring
    # Default Pico pins: GP0 (SDA), GP1 (SCL)
    I2C_BUS = 0  # Use 0 or 1
    I2C_SDA_PIN = 0  # GPIO pin number (GP0 = pin 1)
    I2C_SCL_PIN = 1  # GPIO pin number (GP1 = pin 2)
    SHT31_ADDRESS = 0x44  # Usually 0x44, sometimes 0x45
    
    # mDNS Settings - Usually don't need to change
    MDNS_SERVICE_TYPE = "_trailer-gateway._tcp.local"
    MDNS_SERVICE_INSTANCE = "TrailerAutomationGateway"
