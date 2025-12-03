# Raspberry Pi Pico 2 W - Hardware Wiring Guide

## Pin Assignments
- **RGB LED Red**: GPIO 16 (Pin 21) + 220Ω resistor
- **RGB LED Green**: GPIO 17 (Pin 22) + 220Ω resistor
- **RGB LED Blue**: GPIO 18 (Pin 24) + 220Ω resistor
- **Relay Module**: GPIO 15 (Pin 20)  
- **SHT31 I2C Sensor**: GPIO 4/SDA (Pin 6), GPIO 5/SCL (Pin 7)

## RGB LED Wiring (100FTS-YT-RCB-CA Common Anode)
```
RGB LED (4-pin)           Pico 2 W
Pin 1 (Red Cathode)   ->  GP16 (Pin 21) through 220Ω resistor
Pin 2 (Anode +)       ->  3.3V (Pin 36)
Pin 3 (Green Cathode) ->  GP17 (Pin 22) through 220Ω resistor
Pin 4 (Blue Cathode)  ->  GP18 (Pin 24) through 220Ω resistor
```
**⚠️ IMPORTANT:** 
- Resistors are REQUIRED for each color! Use 220Ω-330Ω resistors.
- **Pin 2 is the common anode (+) - connect to 3.3V.**
- This is **common anode**, so logic is inverted (HIGH=OFF, LOW=ON).

## Relay Module Wiring
```
Relay Module     Pico 2 W
VCC       ->     3.3V (Pin 36) or VBUS (Pin 40)
GND       ->     GND (any GND pin)
IN/Signal ->     GP15 (Pin 20)
```

## SHT31 Sensor Wiring (I2C)
```
SHT31 Sensor     Pico 2 W
VCC/VIN   ->     3.3V (Pin 36) - Do NOT use 5V!
GND       ->     GND (any GND pin)
SDA       ->     GP4 (Pin 6)
SCL       ->     GP5 (Pin 7)
```

## LED Status Indicators
- **Solid RED**: Booting
- **Solid BLUE**: Connecting to WiFi
- **Solid GREEN**: Connected (5 seconds, then off)
- **Blue blinks**: WiFi errors (count = error #)
- **Red blinks**: Network/Gateway errors (count = error #)

## Building & Uploading
1. Edit `data/config.json` with your WiFi credentials
2. Build: `pio run`
3. Upload firmware: Hold BOOTSEL, connect USB, `pio run --target upload`
4. Upload config: `pio run --target uploadfs`
5. Monitor: `pio device monitor` (115200 baud)

## Key Differences from ESP32-S3
- Uses LittleFS instead of SD card
- WiFi uses progressive timeout delays (15s/20s/30s) instead of power levels
- I2C explicitly configured for GP4/GP5
- Uses LEAmDNS library for mDNS
- Serial baud: 115200 (vs 921600 on ESP32)
- **RGB LED**: Uses common anode RGB LED (3 GPIO pins with inverted logic) instead of WS2812 NeoPixel
