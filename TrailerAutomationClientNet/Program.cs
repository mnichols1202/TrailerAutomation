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
        private static HttpClient? _httpClient = null;
        private static GpioRelayController? _gpioController = null;

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

            _httpClient = new HttpClient
            {
                BaseAddress = gatewayUri,
                Timeout = TimeSpan.FromSeconds(5)
            };
            var http = _httpClient;

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
            _gpioController = new GpioRelayController(_config);
            var gpioController = _gpioController;

            // Initialize button controller for physical button support
            Console.WriteLine("Initializing button controller...");
            using var buttonController = new ButtonController(_config, gpioController, http);
            buttonController.StartMonitoring();

            // Start TCP command listener
            Console.WriteLine($"Starting command listener on port {_config.Device.CommandListenerPort}...");
            var commandListener = new CommandListener(_config, gpioController, buttonController);
            var listenerTask = Task.Run(() => commandListener.RunAsync(cts.Token), cts.Token);

            // Send initial heartbeat immediately to establish presence
            Console.WriteLine("Sending initial heartbeat...");
            var needsRegistration = await SendHeartbeatAsync(http);

            // Register device with gateway (includes command port and capabilities)
            if (needsRegistration == true || true) // Always register on startup
            {
                await RegisterDeviceAsync(http);
            }

            // Start background sensor reporting loop (independent of heartbeat)
            Console.WriteLine("Starting sensor loop...");
            _ = Task.Run(() => SensorLoopAsync(http, cts.Token), cts.Token);

            Console.WriteLine("Starting heartbeat loop...");

            var heartbeatInterval = TimeSpan.FromSeconds(_config.Intervals.HeartbeatSeconds);
            int consecutiveFailures = 0;
            const int maxFailuresBeforeRediscovery = 3;
            bool hasRegistered = true; // Track if we've completed initial registration

            while (!cts.Token.IsCancellationRequested)
            {
                await Task.Delay(heartbeatInterval, cts.Token);

                needsRegistration = await SendHeartbeatAsync(http);
                
                if (needsRegistration == null)
                {
                    // Heartbeat failed (network error, timeout, etc.)
                    consecutiveFailures++;
                    Console.WriteLine($"[Heartbeat] Failed ({consecutiveFailures}/{maxFailuresBeforeRediscovery})");
                    
                    if (consecutiveFailures >= maxFailuresBeforeRediscovery)
                    {
                        Console.WriteLine("[Heartbeat] Gateway unreachable - attempting mDNS re-discovery...");
                        
                        var newGatewayUri = await GatewayDiscovery.DiscoverAsync(
                            TimeSpan.FromSeconds(_config.Gateway.DiscoveryTimeoutSeconds));
                        
                        if (newGatewayUri != null && newGatewayUri != _httpClient.BaseAddress)
                        {
                            Console.WriteLine($"[Heartbeat] Gateway moved: {_httpClient.BaseAddress} -> {newGatewayUri}");
                            _httpClient.BaseAddress = newGatewayUri;
                            http = _httpClient;
                            consecutiveFailures = 0;
                            
                            // Re-register with new Gateway location
                            await SendHeartbeatAsync(http, includeRelayStates: true);
                            await RegisterDeviceAsync(http);
                        }
                        else if (newGatewayUri != null)
                        {
                            Console.WriteLine($"[Heartbeat] Gateway still at {newGatewayUri}");
                            consecutiveFailures = 0; // Reset but Gateway still unreachable
                        }
                        else
                        {
                            Console.WriteLine("[Heartbeat] mDNS re-discovery failed - Gateway offline");
                            consecutiveFailures = 0; // Reset to avoid spamming discovery
                        }
                    }
                }
                else
                {
                    // Heartbeat succeeded
                    consecutiveFailures = 0;
                    
                    // If gateway requests re-registration (e.g., it restarted), send relay states and re-register
                    if (needsRegistration.Value)
                    {
                        // Only sync relay states if we've previously registered (this is a RE-registration)
                        if (hasRegistered)
                        {
                            Console.WriteLine("[Heartbeat] Gateway restarted - syncing relay states...");
                            await SendHeartbeatAsync(http, includeRelayStates: true);
                        }
                        else
                        {
                            Console.WriteLine("[Heartbeat] Initial registration requested");
                        }
                        
                        Console.WriteLine("[Heartbeat] Re-registering device with gateway...");
                        await RegisterDeviceAsync(http);
                        hasRegistered = true;
                    }
                }
            }
            
            Console.WriteLine("Shutting down gracefully...");
            await listenerTask;
        }

        private static async Task SensorLoopAsync(HttpClient http, System.Threading.CancellationToken cancellationToken)
        {
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
                Console.WriteLine($"  - [{sensor.Id}] {sensor.Name} at I2C {sensor.I2cAddress} - Interval: {sensor.ReadingIntervalSeconds}s");
            }

            try
            {
                using var sensorReader = new Sht31Reader();

                // Create independent tasks for each sensor with its own interval
                var sensorTasks = sht31Sensors.Select(sensorConfig => 
                    Task.Run(async () =>
                    {
                        var interval = TimeSpan.FromSeconds(sensorConfig.ReadingIntervalSeconds);
                        Console.WriteLine($"[Sensor][{sensorConfig.Id}] Starting with {sensorConfig.ReadingIntervalSeconds}s interval");

                        while (!cancellationToken.IsCancellationRequested)
                        {
                            try
                            {
                                var (temperatureC, humidityPercent) = sensorReader.ReadMeasurement();

                                Console.WriteLine(
                                    $"[{DateTime.Now:T}][{sensorConfig.Id}] {sensorConfig.Name}: " +
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
                                Console.WriteLine($"[{DateTime.Now:T}][{sensorConfig.Id}] Sensor reading sent: {respBody}");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[{DateTime.Now:T}][{sensorConfig.Id}] Sensor loop error: {ex.Message}");
                            }

                            await Task.Delay(interval, cancellationToken);
                        }
                    }, cancellationToken)
                ).ToList();

                // Wait for all sensor tasks to complete
                await Task.WhenAll(sensorTasks);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:T}] Failed to initialize SHT31 sensor: {ex.Message}");
            }
        }

        private static async Task<bool?> SendHeartbeatAsync(HttpClient http, bool includeRelayStates = false)
        {
            try
            {
                object hb;
                
                // Only include relay states when Gateway requests it (after restart)
                if (includeRelayStates)
                {
                    var relayStates = _gpioController?.GetCurrentRelayStates();
                    hb = new
                    {
                        ClientId = _config.Device.ClientId,
                        DeviceType = _config.Device.DeviceType,
                        FriendlyName = _config.Device.FriendlyName,
                        RelayStates = relayStates
                    };
                    Console.WriteLine($"[{DateTime.Now:T}] Heartbeat with relay state sync");
                }
                else
                {
                    hb = new
                    {
                        ClientId = _config.Device.ClientId,
                        DeviceType = _config.Device.DeviceType,
                        FriendlyName = _config.Device.FriendlyName
                    };
                }

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
                return null; // null = network failure, bool = success with/without re-registration
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
                if (_config.Hardware.Buttons.Any(b => b.Enabled))
                {
                    capabilities.Add("button");
                }

                // Build relay info list with current states
                var relays = _config.Hardware.Relays
                    .Select(r => new 
                    { 
                        Id = r.Id, 
                        Name = r.Name,
                        State = _gpioController?.GetRelayState(r.Id) ?? "off"
                    })
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

        // Public method to allow CommandListener to trigger re-registration
        public static async Task TriggerReRegistrationAsync()
        {
            if (_httpClient != null)
            {
                Console.WriteLine("[Init] Triggering device re-registration...");
                await RegisterDeviceAsync(_httpClient);
            }
            else
            {
                Console.WriteLine("[Init] Cannot trigger re-registration: HTTP client not initialized");
            }
        }
    }
}
