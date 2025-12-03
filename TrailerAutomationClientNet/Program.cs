using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TrailerAutomationClientNet
{
    internal class Program
    {
        private static AppConfiguration _config = null!;

        static async Task Main(string[] args)
        {
            Console.WriteLine("TrailerAutomationClientNet starting...");
            Console.WriteLine();

            // Load configuration
            try
            {
                _config = ConfigurationLoader.Load();
                ConfigurationLoader.PrintConfiguration(_config);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load configuration: {ex.Message}");
                return;
            }

            Console.WriteLine("Discovering TrailerAutomationGateway via mDNS...");

            // Discover gateway using your GatewayDiscovery.cs
            Uri? gatewayUri = await GatewayDiscovery.DiscoverAsync(
                TimeSpan.FromSeconds(_config.Gateway.DiscoveryTimeoutSeconds));

            // Fallback to localhost if discovery fails (for testing on same machine)
            if (gatewayUri is null)
            {
                Console.WriteLine("Gateway discovery failed: No TrailerAutomationGateway found.");
                Console.WriteLine("Falling back to localhost:5000 for testing...");
                gatewayUri = new Uri("http://localhost:5000");
            }
            else
            {
                Console.WriteLine($"Discovered Gateway: {gatewayUri}");
            }

            using var http = new HttpClient
            {
                BaseAddress = gatewayUri,
                Timeout = TimeSpan.FromSeconds(5)
            };

            // Set up cancellation for graceful shutdown
            using var cts = new System.Threading.CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
                Console.WriteLine("\nShutdown requested...");
            };

            // Initialize GPIO controller for relay control
            Console.WriteLine("Initializing GPIO controller...");
            using var gpioController = new GpioRelayController(_config);

            // Start TCP command listener
            Console.WriteLine($"Starting command listener on port {_config.Device.CommandListenerPort}...");
            var commandListener = new CommandListener(_config, gpioController);
            var listenerTask = Task.Run(() => commandListener.RunAsync(cts.Token), cts.Token);

            // Send initial heartbeat immediately to establish presence
            Console.WriteLine("Sending initial heartbeat...");
            var needsRegistration = await SendHeartbeatAsync(http);

            // Register device with gateway (includes command port and capabilities)
            if (needsRegistration || true) // Always register on startup
            {
                await RegisterDeviceAsync(http);
            }

            // Start background sensor reporting loop (independent of heartbeat)
            Console.WriteLine("Starting sensor loop...");
            _ = Task.Run(() => SensorLoopAsync(http, cts.Token), cts.Token);

            Console.WriteLine("Starting heartbeat loop...");

            var heartbeatInterval = TimeSpan.FromSeconds(_config.Intervals.HeartbeatSeconds);

            while (!cts.Token.IsCancellationRequested)
            {
                await Task.Delay(heartbeatInterval, cts.Token);

                needsRegistration = await SendHeartbeatAsync(http);
                
                // If gateway requests re-registration (e.g., it restarted), re-register immediately
                if (needsRegistration)
                {
                    Console.WriteLine("[Heartbeat] Re-registering device with gateway...");
                    await RegisterDeviceAsync(http);
                }
            }
            
            Console.WriteLine("Shutting down gracefully...");
            await listenerTask;
        }

        private static async Task SensorLoopAsync(HttpClient http, System.Threading.CancellationToken cancellationToken)
        {
            var sensorReportInterval = TimeSpan.FromSeconds(_config.Intervals.SensorReadingSeconds);
            
            // Find enabled SHT31 sensors in configuration
            var sht31Sensors = _config.Hardware.Sensors
                .Where(s => s.Type.Equals("SHT31", StringComparison.OrdinalIgnoreCase) && s.Enabled)
                .ToList();

            if (sht31Sensors.Count == 0)
            {
                Console.WriteLine("[Sensor] No SHT31 sensors configured or enabled. Sensor loop will not run.");
                return;
            }

            Console.WriteLine($"[Sensor] Found {sht31Sensors.Count} enabled SHT31 sensor(s):");
            foreach (var sensor in sht31Sensors)
            {
                Console.WriteLine($"  - [{sensor.Id}] {sensor.Name} at I2C {sensor.I2cAddress}");
            }

            try
            {
                using var sensor = new Sht31Reader();

                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var (temperatureC, humidityPercent) = sensor.ReadMeasurement();

                        Console.WriteLine(
                            $"[{DateTime.Now:T}] Local sensor reading: " +
                            $"TempC={temperatureC:F2} Humidity={humidityPercent:F2}");

                        var payload = new
                        {
                            ClientId = _config.Device.ClientId,
                            TemperatureC = temperatureC,
                            HumidityPercent = humidityPercent
                        };

                        string json = JsonSerializer.Serialize(payload);
                        using var content = new StringContent(json, Encoding.UTF8, "application/json");

                        var response = await http.PostAsync("/api/sensor-readings", content);
                        response.EnsureSuccessStatusCode();

                        string respBody = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"[{DateTime.Now:T}] Sensor reading sent: {respBody}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[{DateTime.Now:T}] Sensor loop error: {ex.Message}");
                    }

                    await Task.Delay(sensorReportInterval, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:T}] Failed to initialize SHT31 sensor: {ex.Message}");
            }
        }

        private static async Task<bool> SendHeartbeatAsync(HttpClient http)
        {
            try
            {
                var hb = new
                {
                    ClientId = _config.Device.ClientId,
                    DeviceType = _config.Device.DeviceType,
                    FriendlyName = _config.Device.FriendlyName
                };

                string json = JsonSerializer.Serialize(hb);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await http.PostAsync("/api/heartbeat", content);
                response.EnsureSuccessStatusCode();

                string respBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[{DateTime.Now:T}] Heartbeat OK: {respBody}");

                // Check if gateway requests re-registration (e.g., after gateway restart)
                try
                {
                    using var doc = JsonDocument.Parse(respBody);
                    if (doc.RootElement.TryGetProperty("needsRegistration", out var needsReg) && 
                        needsReg.GetBoolean())
                    {
                        Console.WriteLine("[Heartbeat] Gateway requests re-registration");
                        return true; // Signal that re-registration is needed
                    }
                }
                catch
                {
                    // Ignore JSON parse errors - older gateway versions may not have this field
                }

                return false; // No re-registration needed
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:T}] Heartbeat failed: {ex.Message}");
                return false;
            }
        }

        private static async Task RegisterDeviceAsync(HttpClient http)
        {
            try
            {
                Console.WriteLine("[DeviceRegistration] Registering with gateway...");

                // Determine local IP address
                string localIp = GetLocalIpAddress();

                // Build capabilities list
                var capabilities = new List<string>();
                if (_config.Hardware.Relays.Count > 0)
                {
                    capabilities.Add("relay");
                }
                if (_config.Hardware.Sensors.Any(s => s.Enabled))
                {
                    capabilities.Add("temp");
                    capabilities.Add("humidity");
                }

                // Build relay info list (Id and Name only for UI)
                var relays = _config.Hardware.Relays
                    .Select(r => new { Id = r.Id, Name = r.Name })
                    .ToArray();

                var registration = new
                {
                    ClientId = _config.Device.ClientId,
                    DeviceType = _config.Device.DeviceType,
                    FriendlyName = _config.Device.FriendlyName,
                    IpAddress = localIp,
                    CommandPort = _config.Device.CommandListenerPort,
                    Capabilities = capabilities.ToArray(),
                    Relays = relays
                };

                string json = JsonSerializer.Serialize(registration);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await http.PostAsync("/api/devices/register", content);
                response.EnsureSuccessStatusCode();

                string respBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[DeviceRegistration] Success: {respBody}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DeviceRegistration] Failed: {ex.Message}");
                // Don't fail startup if registration fails - will retry on next heartbeat
            }
        }

        private static string GetLocalIpAddress()
        {
            try
            {
                // Try NetworkInterface approach first (works better on Linux)
                var interfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
                foreach (var netInterface in interfaces)
                {
                    // Skip loopback and non-operational interfaces
                    if (netInterface.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up ||
                        netInterface.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                        continue;

                    var props = netInterface.GetIPProperties();
                    foreach (var addr in props.UnicastAddresses)
                    {
                        // Return first IPv4 address that's not loopback
                        if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                            !System.Net.IPAddress.IsLoopback(addr.Address))
                        {
                            return addr.Address.ToString();
                        }
                    }
                }

                // Fallback: socket trick
                using var socket = new System.Net.Sockets.Socket(
                    System.Net.Sockets.AddressFamily.InterNetwork,
                    System.Net.Sockets.SocketType.Dgram,
                    0);
                socket.Connect("8.8.8.8", 65530);
                var endPoint = socket.LocalEndPoint as System.Net.IPEndPoint;
                return endPoint?.Address.ToString() ?? "127.0.0.1";
            }
            catch
            {
                return "127.0.0.1";
            }
        }
    }
}
