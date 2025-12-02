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

            if (gatewayUri is null)
            {
                Console.WriteLine("Gateway discovery failed: No TrailerAutomationGateway found.");
                return;
            }

            Console.WriteLine($"Discovered Gateway: {gatewayUri}");

            using var http = new HttpClient
            {
                BaseAddress = gatewayUri,
                Timeout = TimeSpan.FromSeconds(5)
            };

            // Start TCP command listener
            Console.WriteLine($"Starting command listener on port {_config.Device.CommandListenerPort}...");
            var commandListener = new CommandListener(_config);
            commandListener.Start();

            // Register device with gateway
            await RegisterDeviceAsync(http);

            // Start background sensor reporting loop (independent of heartbeat)
            Console.WriteLine("Starting sensor loop...");
            _ = Task.Run(() => SensorLoopAsync(http));

            Console.WriteLine("Starting heartbeat loop...");

            var heartbeatInterval = TimeSpan.FromSeconds(_config.Intervals.HeartbeatSeconds);

            while (true)
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
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DateTime.Now:T}] Heartbeat failed: {ex.Message}");
                }

                await Task.Delay(heartbeatInterval);
            }
        }

        private static async Task SensorLoopAsync(HttpClient http)
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

                while (true)
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

                    await Task.Delay(sensorReportInterval);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:T}] Failed to initialize SHT31 sensor: {ex.Message}");
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

                var registration = new
                {
                    DeviceId = _config.Device.DeviceId,
                    IpAddress = localIp,
                    CommandPort = _config.Device.CommandListenerPort,
                    Capabilities = capabilities.ToArray()
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
                using var socket = new System.Net.Sockets.Socket(
                    System.Net.Sockets.AddressFamily.InterNetwork,
                    System.Net.Sockets.SocketType.Dgram,
                    0);

                // Connect to a public IP to determine local IP (doesn't actually send data)
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
