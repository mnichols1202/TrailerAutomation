using System;
using System.Threading;
using TrailerAutomationClientNet;

namespace TrailerAutomationClientNet;

public static class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("TrailerAutomationClient-Net (SHT31) starting up...");
        Console.WriteLine("Press Ctrl+C to exit.");
        Console.WriteLine();

        // TODO: Later, read config for gateway address, ports, etc.

        using var reader = new Sht31Reader();

        while (true)
        {
            try
            {
                var (tempC, humidity) = reader.ReadTemperatureAndHumidity();

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Temp: {tempC:F2} °C, Humidity: {humidity:F2} %");

                // TODO: Later, send readings to TrailerAutomationGateway via HTTP/WebSocket/gRPC/etc.
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading SHT31: {ex.Message}");
            }

            Thread.Sleep(TimeSpan.FromSeconds(5));
        }
    }
}
