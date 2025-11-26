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
    }
}
