using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace TrailerAutomationClientNet
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("TrailerAutomationClientNet starting on Raspberry Pi Zero 2 W...");
            Console.WriteLine("Discovering TrailerAutomationGateway via mDNS...");

            var gatewayUri = await GatewayDiscovery.DiscoverAsync(TimeSpan.FromSeconds(5));

            if (gatewayUri is null)
            {
                Console.WriteLine("No TrailerAutomationGateway found via mDNS within timeout.");
                Console.WriteLine("Ensure the gateway is running and on the same network.");
                return;
            }

            Console.WriteLine($"Discovered gateway at: {gatewayUri}");

            using var http = new HttpClient
            {
                BaseAddress = gatewayUri
            };

            // Test the heartbeat endpoint
            try
            {
                Console.WriteLine("Calling /api/heartbeat on the gateway...");
                var response = await http.GetStringAsync("/api/heartbeat");
                Console.WriteLine($"Heartbeat response: {response}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error calling /api/heartbeat on the gateway.");
                Console.WriteLine(ex);
                return;
            }

            // Quick SHT31 smoke test – optional for now
            try
            {
                using var sensor = new Sht31Reader();

                var (tempC, humidityPercent) = sensor.ReadMeasurement();
                Console.WriteLine($"Initial SHT31 reading -> Temp: {tempC:F1} °C, Humidity: {humidityPercent:F1} %");

                // This is where you’ll later add a loop that periodically
                // sends telemetry to the gateway, e.g. /api/telemetry
            }
            catch (Exception ex)
            {
                Console.WriteLine("SHT31 sensor read failed.");
                Console.WriteLine(ex);
            }

            Console.WriteLine("Client run complete. Press ENTER to exit.");
            Console.ReadLine();
        }
    }
}
