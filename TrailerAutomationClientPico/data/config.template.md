# Configuration Template Reference

This file documents the fields in [`data/config.template.json`](config.template.json) and how to turn it into a device-specific `config.json` for upload to LittleFS.

## How to use
1. Copy `data/config.template.json` to `data/config.json`.
2. Replace placeholders with your device's values (especially WiFi credentials and IDs).
3. In PlatformIO, run "Upload File System Image" to push `config.json` to the board's LittleFS.

## Field reference

### WiFi (required)
- `SSID` (string, max 31 chars): network name; must be non-empty.
- `Password` (string, max 63 chars): WPA2/WPA3 passphrase.

### Device (required)
- `ClientId` (string, max 31 chars): unique ID used by the firmware and network services.
- `DeviceType` (string, max 31 chars): identifier such as `PiZero2W` or `PICOW` for logging/metrics.
- `FriendlyName` (string, max 63 chars): human-readable label.
- `CommandListenerPort` (int): TCP port the device listens on (default `8888`).
- `DeviceId` (string): present in the template but currently ignored by the firmware (kept for compatibility).

### Intervals
- `HeartbeatSeconds` (int): how often the device sends heartbeat messages (default `60` if omitted).
- `SensorReadingSeconds` (int): legacy/global interval; currently **unused** because each sensor has its own `ReadingIntervalSeconds`.
- `CommandPollingSeconds` (int): reserved; currently unused by the firmware.

### Gateway
- `DiscoveryTimeoutSeconds` (int): reserved for gateway discovery; currently unused.

### Hardware.Relays (array, optional)
Each entry configures one relay output.
- `Id` (string, max 15 chars): unique relay identifier; required.
- `Name` (string, max 31 chars): display label (default `"Relay"`).
- `Pin` (int): GPIO pin the relay is wired to; required.
- `InitialState` (bool): relay state on boot (`false` = off). Defaults to `false`.
- `Enabled` (bool): whether the relay is active. Defaults to `true`.

### Hardware.Sensors (array, optional)
Each entry configures one sensor.
- `Id` (string, max 15 chars): unique sensor identifier; required.
- `Type` (string, max 15 chars): sensor type code (e.g., `SHT31`); required.
- `Name` (string, max 31 chars): display label (default `"Sensor"`).
- `I2cAddress` (string, max 7 chars): address like `"0x44"`.
- `Enabled` (bool): whether readings are taken (default `false`).
- `ReadingIntervalSeconds` (int): per-sensor polling interval; defaults to `300` (5 minutes) if omitted.

### Hardware.Buttons (array, optional)
Each entry defines a physical button that can target a relay (local or remote).
- `Id` (string, max 15 chars): unique button identifier; required.
- `Name` (string, max 31 chars): display label (default `"Button"`).
- `Pin` (int): GPIO pin the button is wired to; required.
- `TargetDevice` (string, max 31 chars): device ID to send the action to; required when enabled.
- `TargetRelay` (string, max 15 chars): relay ID on the target device; required when enabled.
- `Enabled` (bool): enables the button entry (default `false`).

## Example minimal config
```json
{
  "WiFi": { "SSID": "your-ssid", "Password": "your-pass" },
  "Device": {
    "ClientId": "pico-001",
    "DeviceType": "PICOW",
    "FriendlyName": "Trailer Pico",
    "CommandListenerPort": 8888
  },
  "Intervals": { "HeartbeatSeconds": 60 },
  "Hardware": {
    "Relays": [
      { "Id": "relay1", "Name": "Lights", "Pin": 17, "InitialState": false, "Enabled": true }
    ],
    "Sensors": [
      { "Id": "sht31-main", "Type": "SHT31", "Name": "Temp & Humidity", "I2cAddress": "0x44", "Enabled": true, "ReadingIntervalSeconds": 60 }
    ]
  }
}
```

Notes:
- JSON comments are not allowed; keep values strictly JSON.
- Length limits come from the firmware structs in `include/fsconfig.h`; exceeding them will truncate values.
