using System.Text.Json;

namespace TrailerAutomationThermostatNet;

/// <summary>
/// Configuration loader with validation. Adapted from ClientNet.
/// </summary>
public static class ConfigurationLoader
{
    public static AppConfiguration Load(string configPath = "config.json")
    {
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Configuration file not found: {configPath}");
        }

        var json = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<AppConfiguration>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        });

        if (config == null)
        {
            throw new InvalidOperationException("Failed to deserialize configuration");
        }

        ValidateConfiguration(config);
        return config;
    }

    private static void ValidateConfiguration(AppConfiguration config)
    {
        var validationErrors = new List<string>();

        // Device validation
        if (string.IsNullOrWhiteSpace(config.Device.ClientId))
            validationErrors.Add("Device.ClientId is required");
        if (string.IsNullOrWhiteSpace(config.Device.DeviceType))
            validationErrors.Add("Device.DeviceType is required");

        // Display validation
        if (config.Hardware.Display.Width < 32 || config.Hardware.Display.Width > 256)
            validationErrors.Add("Display.Width must be between 32 and 256");
        if (config.Hardware.Display.Height < 32 || config.Hardware.Display.Height > 256)
            validationErrors.Add("Display.Height must be between 32 and 256");

        // Sensor validation
        if (string.IsNullOrWhiteSpace(config.Hardware.Sensor.Type))
            validationErrors.Add("Sensor.Type is required");

        // Thermostat validation
        if (config.Thermostat.MinSetpoint >= config.Thermostat.MaxSetpoint)
            validationErrors.Add("Thermostat.MinSetpoint must be less than MaxSetpoint");
        if (config.Thermostat.DefaultSetpoint < config.Thermostat.MinSetpoint ||
            config.Thermostat.DefaultSetpoint > config.Thermostat.MaxSetpoint)
            validationErrors.Add("Thermostat.DefaultSetpoint must be between MinSetpoint and MaxSetpoint");

        if (validationErrors.Any())
        {
            throw new InvalidOperationException(
                $"Configuration validation failed:\n{string.Join("\n", validationErrors)}");
        }
    }
}
