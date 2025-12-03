# LittleFS Configuration Setup

The config.json file is now stored in the ESP32's internal flash memory using LittleFS.

## How to Upload Config

1. **Edit config.json** in the `data/` folder with your settings

2. **Upload the filesystem** (this uploads config.json to the ESP32):
   ```bash
   pio run --target uploadfs
   ```

3. **Upload the firmware** (normal upload):
   ```bash
   pio run --target upload
   ```

## Workflow

- **First time setup**: Run both `uploadfs` and `upload`
- **Firmware updates only**: Just run `upload` (config persists)
- **Config changes**: Run `uploadfs` then `upload` (or just `uploadfs` if firmware hasn't changed)

## Benefits Over SD Card

✅ No wiring required  
✅ No SD card compatibility issues  
✅ More reliable (no moving parts)  
✅ Config persists across firmware updates  
✅ Faster access  

## PlatformIO Upload Commands

In VS Code:
- Use PlatformIO sidebar → "Upload Filesystem Image" 
- Then "Upload" for firmware

Or via terminal:
```bash
pio run --target uploadfs    # Upload config.json
pio run --target upload      # Upload firmware
pio device monitor           # Monitor serial output
```
