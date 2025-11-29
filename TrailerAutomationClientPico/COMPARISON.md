# Code Comparison: .NET vs MicroPython

This document shows how the TrailerAutomationClientNet code was converted to MicroPython for the Pico 2 W.

## Architecture Comparison

### .NET Version (Program.cs)
```csharp
using System;
using System.Net.Http;
using System.Threading.Tasks;

internal class Program
{
    static async Task Main(string[] args)
    {
        // Discover gateway
        Uri? gatewayUri = await GatewayDiscovery.DiscoverAsync(TimeSpan.FromSeconds(8));
        
        // HTTP client
        using var http = new HttpClient { BaseAddress = gatewayUri };
        
        // Background task
        _ = Task.Run(() => SensorLoopAsync(http, clientId));
        
        // Main loop
        while (true) {
            await http.PostAsync("/api/heartbeat", content);
            await Task.Delay(HeartbeatInterval);
        }
    }
}
```

### MicroPython Version (main.py)
```python
import urequests as requests
import _thread

def main():
    # Discover gateway
    gateway_url = discover_gateway(timeout_sec=8)
    
    # Background thread
    _thread.start_new_thread(sensor_loop, (gateway_url, client_id))
    
    # Main loop
    while True:
        send_heartbeat(gateway_url, client_id, device_type, friendly_name)
        time.sleep(HEARTBEAT_INTERVAL_SEC)
```

**Key Differences:**
- ❌ No async/await in MicroPython
- ✅ Use `_thread` for background tasks
- ✅ `urequests` instead of `HttpClient`
- ✅ `time.sleep()` instead of `Task.Delay()`

---

## Gateway Discovery Comparison

### .NET (GatewayDiscovery.cs)
```csharp
using Makaretu.Dns;

public static async Task<Uri?> DiscoverAsync(TimeSpan? timeout)
{
    using var mdns = new MulticastService();
    mdns.UseIpv4 = true;
    mdns.UseIpv6 = false;
    
    mdns.AnswerReceived += OnAnswerReceived;
    mdns.Start();
    
    var browse = new Message();
    browse.Questions.Add(new Question { 
        Name = ServiceType, 
        Type = DnsType.PTR 
    });
    mdns.SendQuery(browse);
    
    // Wait for response...
}
```

### MicroPython (gateway_discovery.py)
```python
import socket
import struct

class MDNSQuery:
    def query(self, timeout_sec=5):
        # Create UDP socket
        sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        sock.bind(('', 5353))
        
        # Join multicast group
        mreq = struct.pack('4s4s', 
            socket.inet_aton('224.0.0.251'), 
            socket.inet_aton('0.0.0.0'))
        sock.setsockopt(socket.IPPROTO_IP, 
            socket.IP_ADD_MEMBERSHIP, mreq)
        
        # Send query
        query_packet = self._create_query(self.service_type)
        sock.sendto(query_packet, ('224.0.0.251', 5353))
        
        # Parse responses...
```

**Key Differences:**
- ❌ No high-level mDNS library for MicroPython
- ✅ Custom implementation using raw sockets
- ✅ Manual DNS packet construction/parsing
- ✅ Direct multicast group management

---

## Sensor Reading Comparison

### .NET (Sht31Reader.cs)
```csharp
using System.Device.I2c;
using Iot.Device.Sht3x;

public sealed class Sht31Reader : IDisposable
{
    private readonly I2cDevice _i2cDevice;
    private readonly Sht3x _sensor;
    
    public Sht31Reader(int busId = 1, int deviceAddress = 0x44)
    {
        var settings = new I2cConnectionSettings(busId, deviceAddress);
        _i2cDevice = I2cDevice.Create(settings);
        _sensor = new Sht3x(_i2cDevice);
    }
    
    public (double TemperatureC, double HumidityPercent) ReadMeasurement()
    {
        var temperatureC = _sensor.Temperature.DegreesCelsius;
        var humidityPercent = _sensor.Humidity.Percent;
        return (temperatureC, humidityPercent);
    }
}
```

### MicroPython (sht31_reader.py)
```python
import machine

class Sht31Reader:
    CMD_MEASURE_HIGH_REP = 0x2400
    
    def __init__(self, i2c_id=0, sda_pin=0, scl_pin=1, address=0x44):
        self.address = address
        self.i2c = machine.I2C(
            i2c_id,
            sda=machine.Pin(sda_pin),
            scl=machine.Pin(scl_pin),
            freq=100000
        )
    
    def read_measurement(self):
        # Send measurement command
        self._write_command(self.CMD_MEASURE_HIGH_REP)
        time.sleep_ms(16)
        
        # Read 6 bytes
        data = self.i2c.readfrom(self.address, 6)
        
        # Verify CRC and convert
        temp_raw = (data[0] << 8) | data[1]
        temperature_c = -45.0 + (175.0 * temp_raw / 65535.0)
        
        humid_raw = (data[3] << 8) | data[4]
        humidity_percent = 100.0 * humid_raw / 65535.0
        
        return temperature_c, humidity_percent
```

**Key Differences:**
- ❌ No Iot.Device.Bindings library
- ✅ Custom SHT31 driver implementation
- ✅ Manual I2C commands and data parsing
- ✅ CRC validation implemented manually
- ✅ Direct pin configuration

---

## HTTP Communication Comparison

### .NET (Program.cs)
```csharp
var hb = new
{
    ClientId = clientId,
    DeviceType = deviceType,
    FriendlyName = friendlyName
};

string json = JsonSerializer.Serialize(hb);
using var content = new StringContent(json, Encoding.UTF8, "application/json");

var response = await http.PostAsync("/api/heartbeat", content);
response.EnsureSuccessStatusCode();

string respBody = await response.Content.ReadAsStringAsync();
Console.WriteLine($"Heartbeat OK: {respBody}");
```

### MicroPython (main.py)
```python
payload = {
    "ClientId": client_id,
    "DeviceType": device_type,
    "FriendlyName": friendly_name
}

headers = {"Content-Type": "application/json"}
url = f"{gateway_url}/api/heartbeat"

response = requests.post(url, data=json.dumps(payload), headers=headers)

if response.status_code == 200:
    print(f"Heartbeat OK: {response.text}")

response.close()  # Important: free memory!
```

**Key Differences:**
- ❌ No async HTTP in MicroPython
- ✅ Must manually close responses (memory)
- ✅ Use `json.dumps()` instead of `JsonSerializer`
- ✅ Full URL required (no BaseAddress)
- ✅ Explicit headers dictionary

---

## Threading Comparison

### .NET (Program.cs)
```csharp
// Fire and forget background task
_ = Task.Run(() => SensorLoopAsync(http, clientId));

private static async Task SensorLoopAsync(HttpClient http, string clientId)
{
    using var sensor = new Sht31Reader();
    
    while (true)
    {
        var (temperatureC, humidityPercent) = sensor.ReadMeasurement();
        
        // Send to gateway...
        
        await Task.Delay(SensorReportInterval);
    }
}
```

### MicroPython (main.py)
```python
# Start background thread
_thread.start_new_thread(sensor_loop, (gateway_url, client_id))

def sensor_loop(gateway_url, client_id):
    sensor = Sht31Reader(i2c_id=0, sda_pin=0, scl_pin=1)
    
    while True:
        temperature_c, humidity_percent = sensor.read_measurement()
        
        # Send to gateway...
        
        time.sleep(SENSOR_REPORT_INTERVAL_SEC)
```

**Key Differences:**
- ❌ No Task-based async model
- ✅ Use `_thread.start_new_thread()`
- ✅ Limited to ~2-3 threads (resource constrained)
- ✅ No thread synchronization primitives
- ✅ Pass arguments as tuple to thread function

---

## Device Identity Comparison

### .NET (Program.cs)
```csharp
string clientId = Environment.MachineName;  // e.g., "raspberrypi"
string deviceType = "PiZero2W";
string friendlyName = "Zero2W-SensorNode";
```

### MicroPython (main.py)
```python
client_id = config.CLIENT_ID or machine.unique_id().hex()[:16]  # e.g., "e66118682c374338"
device_type = config.DEVICE_TYPE  # "Pico2W"
friendly_name = config.FRIENDLY_NAME  # "Pico2W-SensorNode"
```

**Key Differences:**
- ❌ No Environment.MachineName in MicroPython
- ✅ Use `machine.unique_id()` for unique ID
- ✅ Configuration via config.py class
- ✅ Manual hostname configuration

---

## Error Handling Comparison

### .NET (Program.cs)
```csharp
try
{
    var response = await http.PostAsync("/api/heartbeat", content);
    response.EnsureSuccessStatusCode();
}
catch (HttpRequestException ex)
{
    Console.WriteLine($"HTTP error: {ex.Message}");
}
catch (Exception ex)
{
    Console.WriteLine($"Heartbeat failed: {ex.Message}");
}
```

### MicroPython (main.py)
```python
try:
    response = requests.post(url, data=json.dumps(payload), headers=headers)
    
    if response.status_code == 200:
        print(f"Heartbeat OK: {response.text}")
    else:
        print(f"Heartbeat failed: HTTP {response.status_code}")
    
    response.close()
except Exception as e:
    print(f"Heartbeat error: {e}")
```

**Key Differences:**
- ❌ No specific exception types (HttpRequestException)
- ✅ Catch generic Exception
- ✅ Manual status code checking
- ✅ Use `sys.print_exception(e)` for full traceback

---

## Memory Management Comparison

### .NET
```csharp
using var http = new HttpClient();  // Automatic disposal
using var sensor = new Sht31Reader();  // IDisposable pattern
using var content = new StringContent(json);  // Garbage collected
```

### MicroPython
```python
response = requests.post(url, ...)
response.close()  # MUST manually close!

# Periodic garbage collection
import gc
gc.collect()  # Free memory when needed

# MicroPython auto-manages most objects, but:
# - Close HTTP responses
# - Close files
# - Close sockets
```

**Key Differences:**
- ❌ No `using` statement or IDisposable
- ✅ Manual resource management critical
- ✅ Limited RAM (~200KB free)
- ✅ `gc.collect()` for manual GC
- ⚠️ Memory leaks more common

---

## Configuration Comparison

### .NET
```csharp
// Hard-coded or appsettings.json
private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(10);
private static readonly TimeSpan SensorReportInterval = TimeSpan.FromSeconds(30);

string clientId = Environment.MachineName;
```

### MicroPython
```python
# config.py
class Config:
    HEARTBEAT_INTERVAL_SEC = 10
    SENSOR_REPORT_INTERVAL_SEC = 30
    WIFI_SSID = "YourNetwork"
    WIFI_PASSWORD = "YourPassword"

# main.py
from config import Config
config = Config()
```

**Key Differences:**
- ❌ No appsettings.json or environment variables
- ✅ Use Python class for configuration
- ✅ Import and instantiate Config
- ✅ WiFi credentials in same config

---

## Platform Capabilities Summary

| Feature | .NET (Pi Zero 2W) | MicroPython (Pico 2W) |
|---------|-------------------|----------------------|
| **RAM** | ~512 MB | ~200 KB free |
| **CPU** | 1 GHz quad-core | 150 MHz dual-core |
| **Threading** | Full TPL/async | Limited `_thread` |
| **Networking** | Full stack | Basic sockets |
| **Libraries** | NuGet ecosystem | Limited stdlib |
| **I2C** | High-level APIs | Direct hardware access |
| **mDNS** | Library available | Custom implementation |
| **HTTP** | HttpClient | urequests (basic) |
| **JSON** | System.Text.Json | json module |
| **Logging** | ILogger, etc. | print() statements |
| **Exceptions** | Rich hierarchy | Generic Exception |
| **Debugging** | Full debugger | REPL + print |

---

## Code Size Comparison

### .NET Project
```
Program.cs          ~140 lines
GatewayDiscovery.cs ~280 lines
Sht31Reader.cs      ~70 lines
────────────────────────────
Total: ~490 lines + large dependencies
Binary: ~80+ MB (with runtime)
```

### MicroPython Project
```
main.py             ~200 lines
gateway_discovery.py ~270 lines
sht31_reader.py     ~170 lines
config.py           ~30 lines
────────────────────────────
Total: ~670 lines, no dependencies
Binary: ~200 KB total
```

**Observations:**
- MicroPython code is slightly longer (no high-level libs)
- But total footprint is ~400x smaller
- More low-level control in MicroPython
- More error-prone (manual CRC, DNS parsing, etc.)

---

## Best Practices Translation

### .NET Best Practices
```csharp
✓ Use async/await throughout
✓ Dispose resources properly (using)
✓ Use strongly-typed exceptions
✓ Leverage dependency injection
✓ Use NuGet packages for common tasks
```

### MicroPython Best Practices
```python
✓ Use try/except liberally
✓ Manually close resources (response.close())
✓ Call gc.collect() periodically
✓ Keep code modular (separate files)
✓ Test components independently
✓ Monitor memory with gc.mem_free()
✓ Use print() for debugging
✓ Handle OSError for network issues
```

---

## Testing Approach Differences

### .NET
- Unit tests (xUnit, NUnit)
- Integration tests
- Mocking frameworks
- CI/CD pipelines
- Automated testing

### MicroPython
- Manual REPL testing
- Individual test scripts (`test_*.py`)
- Interactive debugging
- Serial monitor output
- Component isolation testing

---

## Key Takeaways

1. **MicroPython requires more low-level code** but gives direct hardware control
2. **Memory management is critical** - always close resources
3. **No async/await** - use threads sparingly
4. **Custom implementations** needed for mDNS, sensor drivers
5. **Simpler deployment** - just copy .py files
6. **Better for embedded** - smaller footprint, lower power
7. **Less abstraction** - you see what's happening
8. **More hands-on** - educational and transparent

Both implementations achieve the same goal - reliable sensor monitoring with automatic gateway discovery!
