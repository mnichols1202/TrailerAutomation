# TrailerAutomation — Claude Working Instructions

RV trailer electrical automation system. .NET 9 / Blazor Server gateway on a Raspberry Pi,
two ESP32/Pico firmware clients, and a .NET client node.
GitHub: https://github.com/mnichols1202/TrailerAutomation

---

## How to Work in This Repo

### 1. Think Before Coding

State assumptions explicitly before touching any file. When a request is ambiguous across
the S3 and Pico targets, name the ambiguity and ask — do not silently pick one. When a
simpler approach exists than what was asked for, say so before implementing.

Stop and ask when:
- It's unclear whether a change applies to S3, Pico, gateway, or all three
- The right fix depends on hardware you can't test locally
- A requested change would require touching the Pico flash workflow

### 2. Simplicity First

Minimum code that solves the problem. Nothing speculative.

- No abstractions for single-use code paths
- No error handling for scenarios that cannot happen given the hardware constraints
- No "future-proofing" of embedded firmware — flash space and RAM are finite
- If 200 lines could be 50, rewrite it and note the reduction

The test: would a senior embedded engineer say this is overcomplicated? If yes, simplify.

### 3. Surgical Changes

Touch only what the request requires. The firmware targets (S3 and Pico) have separate
code paths that are intentionally different. Do not "clean up" the other target while
fixing one.

- Do not apply Pico-specific fixes to S3 code, or vice versa
- Match existing style per file (C++ in firmware, C# in gateway)
- If you notice dead code or a smell, mention it — don't delete it unless asked
- Remove only imports/variables that YOUR change made unused

### 4. Goal-Driven Execution

For any multi-step task, state the plan and a verifiable check for each step before
starting:

```
1. [Step] → verify: [how to confirm it worked]
2. [Step] → verify: [how to confirm it worked]
```

For firmware changes, "verify" means: builds without error, expected serial output,
observed hardware behavior. Name which target(s) must be tested.

---

## Repository Structure

```
TrailerAutomation/
├── TrailerAutomationGateway/     .NET 9 / Blazor Server — runs on Raspberry Pi
├── TrailerAutomationClientNet/   .NET client — runs on a second Pi
├── TrailerAutomationClientS3/    ESP32-S3 firmware (PlatformIO / Arduino)
├── TrailerAutomationClientPico/  Pico W / Pico 2W firmware (PlatformIO / Arduino)
├── ThermostatNet/                INCOMPLETE — ignore entirely
├── TrailerAutomation.sln         Solution file — use this one
└── .vscode/                      tasks.json, launch.json (shared across all targets)
```

`TrailerAutomation.slnx` has been deleted. `TrailerAutomation.sln` is the only solution file.

---

## Communication Stack

| Layer | Technology | Detail |
|---|---|---|
| Discovery | mDNS | Gateway advertises `_trailer-gateway._tcp.local` on port 5000 |
| Heartbeat / Registration | HTTP POST | Devices → Gateway `/api/devices/register`, `/api/heartbeat` |
| Commands | TCP | Port 8888, line-delimited JSON |
| Relay state events | Static C# event | `ClientRegistry.OnRelayStateChanged` → Blazor re-render |
| Sensor history | LiteDB | `data/sensor-readings.db` on the Pi |

---

## Gateway — TrailerAutomationGateway/

- ASP.NET Core / Blazor Server, port **5000**
- `ClientRegistry` — in-memory device list, 60 s heartbeat interval, 3 missed = stale
- `SensorReadingRegistry` — in-memory latest-reading cache
- `SensorReadingRepository` — LiteDB persistence
- `DeviceCommandService` — sends TCP commands, 5 s connect + 5 s response timeout
- Blazor pages: `/sensors`, `/devices`, `/history`; root redirects to `/sensors`
- Swagger at `/swagger` (development only)
- Dark-theme UI: CSS variables in `wwwroot/app.css`; Chart.js via `wwwroot/chart-helper.js`

---

## Firmware — Critical Constraints

### ⚠️ Pico W / Pico 2W: ONE-PASS FLASH ONLY

The Pico flash layout places firmware and the LittleFS filesystem partition in the same
flash address space. A firmware-only flash **overwrites config.json**.

**Always use the `buildunified` target**, which combines firmware + LittleFS into a single
`.uf2` image. Never suggest or run `upload` alone on a Pico.

```
# Correct
pio run -e picow   --target buildunified   # then upload the combined .uf2
pio run -e pico2w  --target buildunified

# Wrong — wipes config.json
pio run -e picow   --target upload
```

`data/config.json` must exist before building the unified image.
`littlefs_path.py` (pre-script) validates this and prints a warning if missing.

### ESP32-S3: Two-Pass Flash is Fine

Flash firmware and filesystem independently in any order:

```
pio run -e trailer_automation_client_s3 --target upload    # firmware
pio run -e trailer_automation_client_s3 --target uploadfs  # filesystem
```

`config.json` can be written to the filesystem after the firmware is already running.

---

## Known Bug: Pico mDNS Broken Receive Socket — FIXED

**Symptom:** After a gateway restart, the Pico enters an infinite mDNS query loop. The
gateway sees queries arriving (socket can SEND) but never receives a response (socket
cannot RECEIVE).

**Root cause:** LEAmDNS leaves a broken receive socket after a failed `queryService()` call.

**Fix (applied 2026-04-21):** `discoverGateway()` in
`TrailerAutomationClientPico/src/network.cpp` calls `MDNS.end()` + `delay(100)` +
`MDNS.begin()` at the top of every attempt loop to tear down and rebuild the socket. 5
attempts, 3 s between retries. Retry pacing is internal to the function — no `delay()`
needed in the caller.

**Do NOT apply this pattern to TrailerAutomationClientS3.** ESPmDNS handles socket cleanup
correctly. The S3 codebase must not gain this workaround.

---

## PlatformIO Environments

| Label in tasks.json | Environment | Board | Upload protocol |
|---|---|---|---|
| S3: Build / Flash firmware | `trailer_automation_client_s3` | esp32-s3-devkitc-1 | esptool |
| S3: Flash filesystem | `trailer_automation_client_s3` target `uploadfs` | — | esptool |
| PicoW: Build unified image | `picow` target `buildunified` | rpipicow | mbed (COM16) |
| Pico2W: Build unified image | `pico2w` target `buildunified` | rpipico2w | picotool |

Monitor speed: 115200 (Pico), 921600 (S3).

---

## Config File — data/config.json

Both firmware targets load configuration from LittleFS at boot. The file must be present
in `data/config.json` before building a unified image (Pico) or running `uploadfs` (S3).
A template lives at `data/config.json.template`. If `data/config.json` is missing, the
device halts at boot with an LED error pattern and serial output:
`FATAL: Failed to load LittleFS configuration!`

---

## What Not To Touch

- `ThermostatNet/` — incomplete, ignore
- `TrailerAutomationClientS3/src/` — do not apply Pico-specific fixes here
- `TrailerAutomationClientPico/src/` — do not apply S3-specific patterns here
- `.pio/` — build artifacts, never edit manually
