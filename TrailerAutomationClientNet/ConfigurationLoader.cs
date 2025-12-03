using System;
using System.IO;
using System.Text.Json;

namespace TrailerAutomationClientNet
{
    /// <summary>
    /// Loads and validates the application configuration from config.json.
    /// </summary>
    public static class ConfigurationLoader
    {
        private const string ConfigFileName = "config.json";

        public static AppConfiguration Load()
        {
            string configPath = Path.Combine(AppContext.BaseDirectory, ConfigFileName);

            if (!File.Exists(configPath))
            {
                throw new FileNotFoundException(
                    $"Configuration file not found: {configPath}. " +
                    $"Please create a {ConfigFileName} file in the application directory.");
            }

            try
            {
                string json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<AppConfiguration>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                });

                if (config == null)
                {
                    throw new InvalidOperationException("Failed to deserialize configuration file.");
                }

                ValidateConfiguration(config);
                return config;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Invalid JSON in {ConfigFileName}: {ex.Message}", ex);
            }
        }

        private static void ValidateConfiguration(AppConfiguration config)
        {
            // Validate Device
            if (string.IsNullOrWhiteSpace(config.Device.ClientId))
            {
                throw new InvalidOperationException("Device.ClientId cannot be empty.");
            }

            if (config.Device.CommandListenerPort <= 0 || config.Device.CommandListenerPort > 65535)
            {
                throw new InvalidOperationException("Device.CommandListenerPort must be between 1 and 65535.");
            }

            // Validate Intervals
            if (config.Intervals.HeartbeatSeconds <= 0)
            {
                throw new InvalidOperationException("Intervals.HeartbeatSeconds must be positive.");
            }

            if (config.Intervals.SensorReadingSeconds <= 0)
            {
                throw new InvalidOperationException("Intervals.SensorReadingSeconds must be positive.");
            }

            if (config.Intervals.CommandPollingSeconds <= 0)
            {
                throw new InvalidOperationException("Intervals.CommandPollingSeconds must be positive.");
            }

            // Validate Gateway
            if (config.Gateway.DiscoveryTimeoutSeconds <= 0)
            {
                throw new InvalidOperationException("Gateway.DiscoveryTimeoutSeconds must be positive.");
            }

            // Validate Relays
            foreach (var relay in config.Hardware.Relays)
            {
                if (string.IsNullOrWhiteSpace(relay.Id))
                {
                    throw new InvalidOperationException("Relay.Id cannot be empty.");
                }

                if (relay.Pin <= 0)
                {
                    throw new InvalidOperationException($"Relay '{relay.Id}' has invalid Pin number.");
                }
            }

            // Validate Sensors
            foreach (var sensor in config.Hardware.Sensors)
            {
                if (string.IsNullOrWhiteSpace(sensor.Id))
                {
                    throw new InvalidOperationException("Sensor.Id cannot be empty.");
                }

                if (string.IsNullOrWhiteSpace(sensor.Type))
                {
                    throw new InvalidOperationException($"Sensor '{sensor.Id}' has no Type specified.");
                }
            }
        }

        public static void PrintConfiguration(AppConfiguration config)
        {
            Console.WriteLine("=== Configuration ===");
            Console.WriteLine($"Device:");
            Console.WriteLine($"  ClientId: {config.Device.ClientId}");
            Console.WriteLine($"  DeviceType: {config.Device.DeviceType}");
            Console.WriteLine($"  FriendlyName: {config.Device.FriendlyName}");
            Console.WriteLine($"  Command Listener Port: {config.Device.CommandListenerPort}");
            Console.WriteLine();

            Console.WriteLine($"Intervals:");
            Console.WriteLine($"  Heartbeat: {config.Intervals.HeartbeatSeconds}s");
            Console.WriteLine($"  Sensor Reading: {config.Intervals.SensorReadingSeconds}s");
            Console.WriteLine($"  Command Polling: {config.Intervals.CommandPollingSeconds}s");
            Console.WriteLine();

            Console.WriteLine($"Gateway:");
            Console.WriteLine($"  Discovery Timeout: {config.Gateway.DiscoveryTimeoutSeconds}s");
            Console.WriteLine();

            if (config.Hardware.Relays.Count > 0)
            {
                Console.WriteLine($"Relays ({config.Hardware.Relays.Count}):");
                foreach (var relay in config.Hardware.Relays)
                {
                    Console.WriteLine($"  [{relay.Id}] {relay.Name} - Pin: {relay.Pin}, Initial: {(relay.InitialState ? "ON" : "OFF")}");
                }
                Console.WriteLine();
            }

            if (config.Hardware.Sensors.Count > 0)
            {
                Console.WriteLine($"Sensors ({config.Hardware.Sensors.Count}):");
                foreach (var sensor in config.Hardware.Sensors)
                {
                    Console.WriteLine($"  [{sensor.Id}] {sensor.Name} - Type: {sensor.Type}, I2C: {sensor.I2cAddress}, Enabled: {sensor.Enabled}");
                }
                Console.WriteLine();
            }

            Console.WriteLine("=====================");
            Console.WriteLine();
        }
    }
}
