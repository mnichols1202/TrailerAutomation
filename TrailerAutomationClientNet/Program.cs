using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TrailerAutomationClientNet
{
    internal class Program
    {
        // Heartbeat interval (adjust as needed)
        private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(10);

        // How often to send SHT31 temperature/humidity readings
        private static readonly TimeSpan SensorReportInterval = TimeSpan.FromSeconds(30);

        static async Task Main(string[] args)
        {
            Console.WriteLine("TrailerAutomationClientNet starting...");
            Console.WriteLine("Discovering TrailerAutomationGateway via mDNS...");

            // Discover gateway using your GatewayDiscovery.cs
            Uri? gatewayUri = await GatewayDiscovery.DiscoverAsync(TimeSpan.FromSeconds(8));

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

            // Device identity (customize per device)
            string clientId = Environment.MachineName;      // Pi hostname
            string deviceType = "PiZero2W";
            string friendlyName = "Zero2W-SensorNode";

            Console.WriteLine($"ClientId: {clientId}");
            Console.WriteLine($"DeviceType: {deviceType}");
            Console.WriteLine($"FriendlyName: {friendlyName}");

            // Start background sensor reporting loop (independent of heartbeat)
            Console.WriteLine("Starting sensor loop...");
            _ = Task.Run(() => SensorLoopAsync(http, clientId));

            Console.WriteLine("Starting heartbeat loop...");

            while (true)
            {
                try
                {
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
                    Console.WriteLine($"[{DateTime.Now:T}] Heartbeat OK: {respBody}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DateTime.Now:T}] Heartbeat failed: {ex.Message}");
                }

                await Task.Delay(HeartbeatInterval);
            }
        }

        private static async Task SensorLoopAsync(HttpClient http, string clientId)
        {
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
                            ClientId = clientId,
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

                    await Task.Delay(SensorReportInterval);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:T}] Failed to initialize SHT31 sensor: {ex.Message}");
            }
        }
    }
}
