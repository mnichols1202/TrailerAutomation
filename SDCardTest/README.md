# SD Card Test for ESP32-S3

Simple test program to verify SD card reader wiring and functionality.

## Wiring

```
SD Card Module → ESP32-S3
─────────────────────────
VCC  → 3.3V  (NOT 5V!)
GND  → GND
MOSI → GPIO 11
MISO → GPIO 13
SCK  → GPIO 12
CS   → GPIO 10
```

## Build and Upload

```bash
cd SDCardTest
pio run --target upload
pio device monitor
```

## What It Tests

1. ✅ SPI initialization with correct pins
2. ✅ SD card detection and mounting
3. ✅ Card type and size information
4. ✅ Directory listing
5. ✅ File reading (config.json if exists)
6. ✅ File writing (creates test.txt)

## Expected Output

```
=================================
ESP32-S3 SD Card Test
=================================

Pin Configuration:
  CS   (Chip Select): GPIO 10
  MOSI (Master Out) : GPIO 11
  MISO (Master In)  : GPIO 13
  SCK  (Clock)      : GPIO 12

Initializing SPI...
Attempting SD card initialization...
✅ SD Card Mounted Successfully!

=== SD Card Information ===
Card Type: SDHC
Card Size: 7580 MB
Total Space: 7580 MB
Used Space: 1 MB

=== Root Directory ===
Listing directory: /
  FILE: config.json    SIZE: 421
  FILE: test.txt       SIZE: 43

=================================
SD Card Test Complete!
=================================
```

## Troubleshooting

### "SD Card Mount Failed"

1. **Check voltage**: Must be 3.3V, NOT 5V!
2. **Check wiring**: Verify each pin connection
3. **Check SD card**:
   - Is it inserted fully?
   - Is it formatted as FAT32?
   - Try a different card (some cards don't work well)
4. **Check connections**: Loose wires, bad breadboard contacts
5. **Try different SPI speed**: Code tries 4MHz then 1MHz

### Wiring Double-Check

- **VCC → 3.3V**: Red wire to 3.3V pin (NOT 5V!)
- **GND → GND**: Black wire to any GND pin
- **MOSI → GPIO 11**: Data from ESP32 to SD card
- **MISO → GPIO 13**: Data from SD card to ESP32
- **SCK → GPIO 12**: Clock signal
- **CS → GPIO 10**: Chip select (can be any GPIO)

### SD Card Issues

- **Format**: Must be FAT32 (not exFAT, not NTFS)
- **Size**: Works best with ≤32GB cards
- **Quality**: Some cheap/fake cards don't work
- **Speed Class**: Any class works (Class 4, 10, etc.)

### Still Not Working?

1. Try the card in a computer to verify it works
2. Swap MOSI/MISO if you have them backwards
3. Check for bent pins on the SD module
4. Measure voltage at VCC pin (should be 3.3V)
5. Try connecting CS to a different GPIO pin
