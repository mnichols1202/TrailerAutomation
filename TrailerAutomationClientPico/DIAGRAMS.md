# System Architecture Diagrams

## Overall System Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        Local Network                             │
│                                                                   │
│  ┌──────────────────┐         ┌─────────────────────────┐       │
│  │  Pico 2 W Client │◄───────►│ TrailerAutomation       │       │
│  │  (MicroPython)   │  HTTP   │ Gateway                 │       │
│  │                  │         │ (ASP.NET Core)          │       │
│  │  - WiFi          │  mDNS   │                         │       │
│  │  - SHT31 Sensor  │ ◄─────► │ - Service Discovery     │       │
│  │  - Heartbeat     │         │ - API Endpoints         │       │
│  │  - Sensor Data   │         │ - Data Processing       │       │
│  └──────────────────┘         └─────────────────────────┘       │
│                                                                   │
└─────────────────────────────────────────────────────────────────┘
```

## Hardware Connection Diagram

```
┌─────────────────────────────────────────────────────┐
│                 Raspberry Pi Pico 2 W                │
│                                                      │
│  ┌──────────┐                                       │
│  │   RP2040 │   Built-in                            │
│  │   CPU    │◄─────────► WiFi Chip                  │
│  │          │            (CYW43439)                  │
│  └────┬─────┘                                        │
│       │                                              │
│   I2C Bus (I2C0)                                     │
│       │                                              │
│   ┌───┴────┐                                         │
│   │  Pin 1 │ GP0 (SDA) ────────────────┐            │
│   │  Pin 2 │ GP1 (SCL) ──────────────┐ │            │
│   │  Pin 36│ 3.3V ─────────────────┐ │ │            │
│   │  Pin 38│ GND ────────────────┐ │ │ │            │
│   └────────┘                     │ │ │ │            │
└──────────────────────────────────┼─┼─┼─┼────────────┘
                                   │ │ │ │
                         ┌─────────┼─┼─┼─┼────────┐
                         │         │ │ │ │  SHT31 │
                         │   GND ◄─┘ │ │ │        │
                         │   VCC ◄───┘ │ │  Temp/ │
                         │   SCL ◄─────┘ │  Humid │
                         │   SDA ◄───────┘ Sensor │
                         └─────────────────────────┘
```

## Software Architecture

```
┌─────────────────────────────────────────────────────────┐
│                      main.py                             │
│                                                          │
│  ┌────────────────────────────────────────────────┐    │
│  │  main() - Entry Point                          │    │
│  │  ├─ connect_wifi()                             │    │
│  │  ├─ discover_gateway() ──────┐                 │    │
│  │  ├─ _thread.start_new_thread()│                │    │
│  │  │   └─ sensor_loop()         │                │    │
│  │  └─ while True: heartbeat     │                │    │
│  └────────┬─────────────────┬────┴────────────────┘    │
│           │                 │         │                 │
└───────────┼─────────────────┼─────────┼─────────────────┘
            │                 │         │
            ▼                 ▼         ▼
┌──────────────────┐  ┌──────────────────┐  ┌─────────────┐
│  config.py       │  │ gateway_discovery │  │ sht31_reader│
│                  │  │     .py           │  │    .py      │
│  - WiFi SSID     │  │                   │  │             │
│  - WiFi Password │  │  MDNSQuery        │  │ Sht31Reader │
│  - Device ID     │  │  - query()        │  │ - read()    │
│  - Intervals     │  │  - parse()        │  │ - CRC check │
│  - I2C Pins      │  │  - resolve()      │  │ - convert() │
└──────────────────┘  └──────────────────┘  └─────────────┘
```

## Data Flow Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                       Pico 2 W                               │
│                                                              │
│  Main Thread                    Background Thread           │
│  ────────────                   ─────────────────           │
│                                                              │
│  ┌──────────────┐              ┌──────────────┐            │
│  │ Start        │              │ sensor_loop()│            │
│  └──────┬───────┘              └──────┬───────┘            │
│         │                              │                    │
│         ▼                              ▼                    │
│  ┌──────────────┐              ┌──────────────┐            │
│  │ Connect WiFi │              │ Init SHT31   │            │
│  └──────┬───────┘              └──────┬───────┘            │
│         │                              │                    │
│         ▼                              ▼                    │
│  ┌──────────────┐              ┌──────────────┐            │
│  │ Discover     │              │ Read Sensor  │            │
│  │ Gateway      │              └──────┬───────┘            │
│  │ (mDNS)       │                     │                    │
│  └──────┬───────┘                     ▼                    │
│         │                      ┌──────────────┐            │
│         ▼                      │ POST /api/   │            │
│  ┌──────────────┐              │ sensor-      │            │
│  │ POST /api/   │              │ readings     │            │
│  │ heartbeat    │              └──────┬───────┘            │
│  └──────┬───────┘                     │                    │
│         │                             │                    │
│         ▼                             ▼                    │
│  ┌──────────────┐              ┌──────────────┐            │
│  │ Sleep 10s    │              │ Sleep 30s    │            │
│  └──────┬───────┘              └──────┬───────┘            │
│         │                             │                    │
│         └───────────┐     ┌───────────┘                    │
│                     ▼     ▼                                │
│              ┌──────────────┐                              │
│              │ Loop Forever │                              │
│              └──────────────┘                              │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

## Network Communication

```
Pico 2 W                                         Gateway
────────                                         ────────

1. mDNS Discovery
   │
   ├──► Multicast UDP 224.0.0.251:5353
   │    Query: _trailer-gateway._tcp.local
   │
   │◄── Response: SRV + A records
   │    IP: 192.168.1.100, Port: 5000
   │

2. Heartbeat (every 10s)
   │
   ├──► HTTP POST /api/heartbeat
   │    {
   │      "ClientId": "e661...",
   │      "DeviceType": "Pico2W",
   │      "FriendlyName": "Pico2W-Node"
   │    }
   │
   │◄── Response: 200 OK
   │    {"status": "ok"}
   │

3. Sensor Data (every 30s)
   │
   ├──► HTTP POST /api/sensor-readings
   │    {
   │      "ClientId": "e661...",
   │      "TemperatureC": 22.45,
   │      "HumidityPercent": 45.67
   │    }
   │
   │◄── Response: 200 OK
   │    {"status": "ok"}
   │
```

## Thread Execution Timeline

```
Time  Main Thread              Background Thread
────  ──────────────────────   ────────────────────────
0s    Boot
1s    Connect WiFi
2s    ├─ Connected
3s    Discover Gateway
4s    ├─ Found
5s    Start background thread ──► Init SHT31
6s                                ├─ Initialized
7s    Send heartbeat ✓
8s                                Read sensor ✓
9s                                Send data ✓
10s                               Sleep...
17s   Send heartbeat ✓
27s   Send heartbeat ✓
36s                               Wake up
37s   Send heartbeat ✓            Read sensor ✓
38s                               Send data ✓
39s                               Sleep...
47s   Send heartbeat ✓
...   (continues forever)
```

## State Machine Diagram

```
                    ┌──────────────┐
                    │   STARTUP    │
                    └──────┬───────┘
                           │
                           ▼
                    ┌──────────────┐
            ┌──────►│ CONNECTING   │
            │       │   TO WIFI    │
            │       └──────┬───────┘
            │              │
            │              ▼
            │       ┌──────────────┐
            │  ┌───►│ DISCOVERING  │
            │  │    │   GATEWAY    │
            │  │    └──────┬───────┘
            │  │           │
            │  │           ▼
            │  │    ┌──────────────┐
            │  │    │   RUNNING    │◄────┐
            │  │    │              │     │
            │  │    │ - Heartbeat  │     │
            │  │    │ - Sensor     │     │
            │  │    └──────┬───────┘     │
            │  │           │              │
            │  │           ▼              │
            │  │    ┌──────────────┐     │
            │  │    │    ERROR     │     │
            │  │    │              │     │
            │  │    │ - WiFi lost  │     │
            │  │    │ - HTTP fail  │     │
            │  │    │ - Sensor err │     │
            │  │    └──────┬───────┘     │
            │  │           │              │
            │  │           ▼              │
            │  │    ┌──────────────┐     │
            │  └────┤  RETRYING    │─────┘
            │       └──────┬───────┘
            │              │
            │              ▼
            └────────(restart)
```

## Memory Layout

```
┌────────────────────────────────────────────────┐
│          Pico 2 W Memory (264 KB SRAM)         │
├────────────────────────────────────────────────┤
│  MicroPython Runtime        │ ~100 KB          │
├─────────────────────────────┼──────────────────┤
│  Application Code           │  ~20 KB          │
│  - main.py                  │                  │
│  - gateway_discovery.py     │                  │
│  - sht31_reader.py          │                  │
│  - config.py                │                  │
├─────────────────────────────┼──────────────────┤
│  Network Stack              │  ~80 KB          │
│  - WiFi driver              │                  │
│  - TCP/IP stack             │                  │
│  - HTTP client              │                  │
├─────────────────────────────┼──────────────────┤
│  Working Memory (Heap)      │  ~60 KB          │
│  - Variables                │                  │
│  - Buffers                  │                  │
│  - Temporary objects        │                  │
├─────────────────────────────┼──────────────────┤
│  Thread Stack #2            │   ~4 KB          │
│  (sensor_loop)              │                  │
└─────────────────────────────┴──────────────────┘

Free RAM Available: ~60-80 KB (after initialization)
```

## I2C Communication Flow

```
Pico 2 W                              SHT31
────────                              ─────

1. Initialize
   │
   ├──► I2C Bus Scan (0x44)
   │
   │◄── ACK (device present)
   │

2. Soft Reset
   │
   ├──► Write: 0x30A2
   │
   │◄── ACK
   │
   ├──► Wait 10ms
   │

3. Read Measurement
   │
   ├──► Write: 0x2400 (high repeatability)
   │
   │◄── ACK
   │
   ├──► Wait 16ms
   │
   ├──► Read: 6 bytes
   │
   │◄── [Temp MSB][Temp LSB][Temp CRC]
   │    [Hum MSB][Hum LSB][Hum CRC]
   │
   ├──► Verify CRC-8
   │
   ├──► Convert to °C and %
   │
   └──► Return (22.45, 45.67)
```

## Error Recovery Flow

```
                ┌──────────────┐
                │  Operation   │
                └──────┬───────┘
                       │
                       ▼
                ┌──────────────┐
                │   Try Task   │
                └──────┬───────┘
                       │
                       ├───► Success ───► Continue
                       │
                       ├───► WiFi Error
                       │      │
                       │      └──► Reconnect WiFi
                       │           │
                       │           ├─ Success ──► Retry Task
                       │           │
                       │           └─ Fail ────► Reset Device
                       │
                       ├───► HTTP Error
                       │      │
                       │      └──► Rediscover Gateway
                       │           │
                       │           ├─ Found ───► Retry Task
                       │           │
                       │           └─ Not Found ► Wait & Retry
                       │
                       └───► Sensor Error
                              │
                              └──► Reinit Sensor
                                   │
                                   ├─ Success ──► Continue
                                   │
                                   └─ Fail ────► Log & Skip
```

## File Dependencies

```
main.py
  │
  ├─► config.py
  │     │
  │     └─► (no dependencies)
  │
  ├─► gateway_discovery.py
  │     │
  │     ├─► socket (built-in)
  │     ├─► struct (built-in)
  │     └─► network (built-in)
  │
  ├─► sht31_reader.py
  │     │
  │     ├─► machine (built-in)
  │     └─► time (built-in)
  │
  ├─► urequests (built-in)
  ├─► json (built-in)
  ├─► time (built-in)
  ├─► machine (built-in)
  ├─► network (built-in)
  └─► _thread (built-in)

test_*.py files
  │
  └─► (import relevant modules)

All dependencies are built into MicroPython!
No external libraries needed! ✨
```

## Deployment Workflow

```
Developer Machine                    Pico 2 W
───────────────────                  ────────

1. Prepare Files
   │
   ├─ Edit config.py
   ├─ Verify code
   └─ Open Thonny IDE
      │
      └──────────────────────┐
                             │
2. Flash MicroPython         │
   │                         │
   ├─ Download .uf2          │
   ├─ Hold BOOTSEL           │
   └─ Copy .uf2 ───────────► │ MicroPython Installed
                             │
3. Upload Files              │
   │                         │
   ├─ config.py ───────────► │ Stored in flash
   ├─ gateway_discovery.py ─► │ Stored in flash
   ├─ sht31_reader.py ──────► │ Stored in flash
   └─ main.py ──────────────► │ Stored in flash
                                  │
4. Test                           │
   │                              ▼
   ├─ Run test scripts        ┌──────────┐
   └─ Verify output           │  Tests   │
                              │  Pass ✓  │
5. Deploy                     └────┬─────┘
   │                               │
   └─ Disconnect USB               ▼
      Connect power            ┌──────────┐
                               │ Auto-Run │
                               │ main.py  │
                               └──────────┘
```
