using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace TrailerAutomationClientNet
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Discovering TrailerAutomationGateway via mDNS...");

            var result = await GatewayDiscovery.DiscoverAsync(TimeSpan.FromSeconds(5));

            if (result is null)
            {
                Console.WriteLine("Gateway not found (mDNS discovery timed out).");
                return;
            }

            var (ip, port) = result.Value;
            var baseUrl = $"http://{ip}:{port}";

            Console.WriteLine($"Gateway found at {baseUrl}");

            using var http = new HttpClient { BaseAddress = new Uri(baseUrl) };

            try
            {
                var response = await http.GetStringAsync("/api/heartbeat");
                Console.WriteLine($"Heartbeat: {response}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error calling /api/heartbeat");
                Console.WriteLine(ex.Message);
            }

            // Your SHT31 loop / telemetry sending would go here next.
        }
    }
}
