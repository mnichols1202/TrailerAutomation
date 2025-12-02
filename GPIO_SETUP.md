# GPIO LED Control Setup Guide

## What Was Added

✅ **System.Device.Gpio** NuGet package installed  
✅ **GpioController.cs** - New GPIO relay controller class  
✅ **CommandListener.cs** - Updated to control real GPIO pins  
✅ **Program.cs** - Initializes GPIO controller on startup

## How It Works

### 1. On Startup
```
[GPIO] Initializing relay1 on pin 17 (initial: False)
[GPIO] Initializing relay2 on pin 27 (initial: False)
[GPIO] Initialized 2 relay pin(s)
```

Both LEDs start **OFF** (pins set to LOW/0V)

### 2. When Command Received
```
Gateway → TCP Command → CommandListener → GpioController → GPIO Pin → LED
```

**Example Command:**
```json
{
  "commandId": "abc-123",
  "type": "setRelay",
  "payload": {
    "relayId": "relay1",
    "state": "on"
  }
}
```

**Result:**
- GPIO pin 17 set to HIGH (3.3V)
- LED connected to pin 17 lights up 💡

### 3. Console Output
```
[CommandListener] SetRelay: relay1 -> on (Pin: 17)
[GPIO] relay1 (pin 17) -> on (High)
```

## LED Wiring Quick Reference

```
Pi GPIO 17 (Pin 11) ───→ LED+ ─→ LED- ─→ [220Ω] ─→ GND
Pi GPIO 27 (Pin 13) ───→ LED+ ─→ LED- ─→ [220Ω] ─→ GND
Pi Ground  (Pin 6)  ────────────────────────────→ GND
```

## Testing

### On Windows (Development)
When running on Windows (non-Pi hardware), you'll see:
```
[GPIO] Warning: Failed to initialize GPIO controller: ...
[GPIO] GPIO control will be simulated (no hardware control)
[GPIO] SIMULATED: relay1 (pin 17) -> on
```

The app works, but doesn't actually control pins.

### On Raspberry Pi
When running on Pi with LEDs connected:
```
[GPIO] Initializing relay1 on pin 17 (initial: False)
[GPIO] Initializing relay2 on pin 27 (initial: False)
[GPIO] relay1 (pin 17) -> on (High)
```

LEDs physically turn on/off! 💡

## Control from Blazor UI

1. Open Gateway web UI: `http://gateway-ip:5000/devices`
2. Find your Pi device (e.g., "pi-zero-001")
3. Click buttons:
   - **💡 Light ON** → relay1 → pin 17 → LED 1 turns ON
   - **🌑 Light OFF** → relay1 → pin 17 → LED 1 turns OFF
   - **🌀 Fan ON** → relay2 → pin 27 → LED 2 turns ON
   - **⭕ Fan OFF** → relay2 → pin 27 → LED 2 turns OFF

## Graceful Shutdown

When you stop the client (Ctrl+C), the GPIO controller:
1. Sets all pins to LOW (turns off all LEDs)
2. Disposes GPIO resources cleanly

```
[GPIO] Disposing GPIO controller...
[GPIO] Turned off relay1 (pin 17)
[GPIO] Turned off relay2 (pin 27)
[GPIO] GPIO controller disposed
```

## Safety Features

- ✅ All pins initialized as OUTPUT
- ✅ Initial state from config.json (default: off)
- ✅ Automatic cleanup on shutdown
- ✅ Simulation mode when GPIO unavailable
- ✅ Error handling for pin write failures

## Next Steps

1. **Wire the LEDs** to pins 17 and 27 (with 220Ω resistors!)
2. **Deploy to Pi**: 
   ```bash
   dotnet publish -c Release -r linux-arm64 --self-contained
   ```
3. **Copy to Pi** and run
4. **Test from Blazor UI** - watch LEDs respond!

## Troubleshooting

**"Permission denied" errors on Pi:**
```bash
# Add user to gpio group
sudo usermod -aG gpio $USER
# Reboot
sudo reboot
```

**LEDs always on/reversed:**
- Check LED polarity (long leg to GPIO, short leg to resistor)
- Verify resistor is between LED and ground

**No GPIO control:**
- Check console for "[GPIO] SIMULATED" messages
- Ensure running on actual Raspberry Pi hardware
- Verify System.Device.Gpio package installed
