# TrailerAutomationClientNet

.NET-based client for the TrailerAutomation RV ecosystem. Runs on Raspberry Pi (and other platforms) to monitor sensors and control relays.

## Configuration

The application uses `config.json` for all configuration. Copy `config.template.json` to `config.json` and customize for your deployment.

### Device Settings
- **ClientId**: Unique identifier for this device (default: machine hostname)
- **DeviceType**: Type of device (e.g., "PiZero2W", "Pi4", etc.)
- **FriendlyName**: Human-readable name shown in the Gateway UI

### Intervals
- **HeartbeatSeconds**: How often to send heartbeat to Gateway (default: 10)
- **SensorReadingSeconds**: How often to read and report sensor data (default: 30)
- **CommandPollingSeconds**: How often to check for commands from Gateway (default: 3)

### Gateway Settings
- **DiscoveryTimeoutSeconds**: How long to wait for mDNS discovery (default: 8)

### Hardware Configuration

#### Relays
Define GPIO-connected relays:
```json
"Relays": [
  {
    "Id": "relay1",
    "Name": "Main Light",
    "Pin": 17,
    "InitialState": false
  }
]
```
- **Id**: Unique identifier for this relay
- **Name**: Human-readable name
- **Pin**: GPIO pin number (BCM numbering)
- **InitialState**: true = ON at startup, false = OFF at startup

#### Sensors
Define I2C-connected sensors:
```json
"Sensors": [
  {
    "Id": "sht31-main",
    "Type": "SHT31",
    "Name": "Temperature & Humidity",
    "I2cAddress": "0x44",
    "Enabled": true
  }
]
```
- **Id**: Unique identifier for this sensor
- **Type**: Sensor type ("SHT31" currently supported)
- **Name**: Human-readable name
- **I2cAddress**: I2C address of the sensor (typically 0x44 or 0x45 for SHT31)
- **Enabled**: true = sensor active, false = sensor disabled

## Running

```bash
dotnet run
```

Or on Raspberry Pi:
```bash
dotnet publish -c Release -r linux-arm64
./bin/Release/net9.0/linux-arm64/publish/TrailerAutomationClientNet
```

## Requirements

- .NET 9.0
- I2C enabled (for sensors): `sudo raspi-config` → Interface Options → I2C
- GPIO access (for relays): Run as sudo or add user to `gpio` group
