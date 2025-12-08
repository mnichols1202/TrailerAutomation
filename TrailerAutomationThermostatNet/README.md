# TrailerAutomation Thermostat (.NET)

Wall-mounted thermostat controller for RV climate control using Raspberry Pi Zero 2 W.

## Hardware

- **Controller**: Raspberry Pi Zero 2 W
- **Display**: Waveshare 1.5" RGB OLED (128x128, SSD1351)
- **Sensor**: SHT31 Temperature/Humidity (I2C)
- **Buttons**: 4 tactile buttons (Up, Down, Mode, Select)
- **Communication**: UART serial to ESP32-S3 relay box

## Features

- Color-coded status display (Blue=Cooling, Red=Heating, Green=Idle)
- Automatic temperature control with hysteresis
- Multiple operating modes (Off, Cool, Heat, Auto)
- Screen timeout and burn-in mitigation
- Gateway integration via WiFi
- Real-time temperature/humidity monitoring
- Safety interlocks (compressor delay, min/max runtime)

## Architecture

```
Wall Thermostat (This Project)          Relay Box (ESP32-S3)
├─ RGB OLED Display                     ├─ 4x Relays
├─ SHT31 Sensor                         │  ├─ A/C Compressor
├─ 4 Buttons                            │  ├─ A/C Fan
├─ UART ←──────────────────────────────►│  ├─ Furnace Blower
└─ WiFi to Gateway                      └─ └─ Furnace Gas Valve
```

## Configuration

Edit `config.json` to configure:
- Device identity and network settings
- Display parameters (brightness, timeout)
- Sensor configuration
- Button mappings
- Thermostat behavior (setpoint, hysteresis, delays)
- UART communication settings

## Building

```powershell
# Restore packages
dotnet restore

# Build for Raspberry Pi (linux-arm)
dotnet publish -c Release -r linux-arm

# Output: bin/Release/net9.0/linux-arm/publish/
```

## Deployment

```powershell
# Copy to Raspberry Pi
scp -r bin/Release/net9.0/linux-arm/publish/* pi@thermostat.local:/home/pi/thermostat/

# SSH and run
ssh pi@thermostat.local
cd ~/thermostat
./TrailerAutomationThermostatNet
```

## Systemd Service

Create `/etc/systemd/system/thermostat.service`:

```ini
[Unit]
Description=Trailer Automation Thermostat
After=network.target

[Service]
Type=simple
User=pi
WorkingDirectory=/home/pi/thermostat
ExecStart=/home/pi/thermostat/TrailerAutomationThermostatNet
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
```

Enable and start:
```bash
sudo systemctl enable thermostat
sudo systemctl start thermostat
sudo systemctl status thermostat
```

## Dependencies

- .NET 9.0 Runtime
- System.Device.Gpio (GPIO and I2C)
- Iot.Device.Bindings (SSD1351, SHT31 drivers)
- System.IO.Ports (UART serial communication)

## Pin Assignments (Raspberry Pi Zero 2 W)

| Function | GPIO | Pin | Notes |
|----------|------|-----|-------|
| Display SPI MOSI | GPIO10 | 19 | SPI0 MOSI |
| Display SPI SCLK | GPIO11 | 23 | SPI0 SCLK |
| Display CS | GPIO8 | 24 | Chip Select |
| Display DC | GPIO25 | 22 | Data/Command |
| Display RST | GPIO24 | 18 | Reset |
| Sensor SDA | GPIO2 | 3 | I2C Data |
| Sensor SCL | GPIO3 | 5 | I2C Clock |
| Button Up | GPIO17 | 11 | Pull-up |
| Button Down | GPIO27 | 13 | Pull-up |
| Button Mode | GPIO22 | 15 | Pull-up |
| Button Select | GPIO23 | 16 | Pull-up |
| UART TX | GPIO14 | 8 | To relay box |
| UART RX | GPIO15 | 10 | From relay box |

## Related Projects

- **TrailerAutomationGateway** - Central hub (ASP.NET Core Blazor)
- **TrailerAutomationClientNet** - Generic .NET client (base for this project)
- **TrailerAutomationClientS3** - ESP32-S3 relay controller
- **TrailerAutomationClientPico** - Pico W/2W relay controller
