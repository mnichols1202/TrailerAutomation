using System.ComponentModel.DataAnnotations;

namespace TrailerAutomationThermostatNet;

/// <summary>
/// Root configuration model for the TrailerAutomation thermostat client.
/// Extends ClientNet functionality with thermostat-specific features.
/// </summary>
public class AppConfiguration
{
    public DeviceConfig Device { get; set; } = new();
    public IntervalsConfig Intervals { get; set; } = new();
    public GatewayConfig Gateway { get; set; } = new();
    public HardwareConfig Hardware { get; set; } = new();
    public ThermostatConfig Thermostat { get; set; } = new();
}

public class DeviceConfig
{
    [Required(ErrorMessage = "ClientId is required")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "ClientId must be 1-50 characters")]
    public string ClientId { get; set; } = Environment.MachineName;
    
    [Required]
    [StringLength(50)]
    public string DeviceType { get; set; } = "Thermostat";
    
    [Required]
    [StringLength(100)]
    public string FriendlyName { get; set; } = "Wall Thermostat";
    
    [Range(1, 65535, ErrorMessage = "CommandListenerPort must be between 1 and 65535")]
    public int CommandListenerPort { get; set; } = 8888;
}

public class IntervalsConfig
{
    [Range(5, 600, ErrorMessage = "HeartbeatSeconds must be between 5 and 600")]
    public int HeartbeatSeconds { get; set; } = 60;
    
    [Range(1, 60, ErrorMessage = "CommandPollingSeconds must be between 1 and 60")]
    public int CommandPollingSeconds { get; set; } = 3;
    
    [Range(5, 60, ErrorMessage = "SensorReadingSeconds must be between 5 and 60")]
    public int SensorReadingSeconds { get; set; } = 10;
    
    [Range(50, 1000, ErrorMessage = "DisplayUpdateMs must be between 50 and 1000")]
    public int DisplayUpdateMs { get; set; } = 100;
}

public class GatewayConfig
{
    public int DiscoveryTimeoutSeconds { get; set; } = 8;
}

public class HardwareConfig
{
    public DisplayConfig Display { get; set; } = new();
    public SensorConfig Sensor { get; set; } = new();
    public UartConfig Uart { get; set; } = new();
}

public class DisplayConfig
{
    [Required]
    [StringLength(20)]
    public string Type { get; set; } = "ILI9341";
    
    [Range(32, 480)]
    public int Width { get; set; } = 320;
    
    [Range(32, 320)]
    public int Height { get; set; } = 240;
    
    [Range(0, 1)]
    public int DisplaySpiDevice { get; set; } = 0;
    
    [Range(0, 100)]
    public int DisplaySpiChipSelect { get; set; } = 8;
    
    [Range(0, 1)]
    public int TouchSpiDevice { get; set; } = 1;
    
    [Range(0, 100)]
    public int TouchSpiChipSelect { get; set; } = 7;
    
    [Range(0, 100)]
    public int DataCommandPin { get; set; } = 25;
    
    [Range(0, 100)]
    public int ResetPin { get; set; } = 24;
    
    [Range(0, 100)]
    public int BacklightPin { get; set; } = 23;
    
    public int? TouchIrqPin { get; set; } = 22;
    
    [Range(0, 300)]
    public int TimeoutSeconds { get; set; } = 30;
}

public class SensorConfig
{
    [Required]
    [StringLength(20)]
    public string Type { get; set; } = "SHT31";
    
    [Range(0, 10)]
    public int I2cBus { get; set; } = 1;
    
    [Range(0, 127)]
    public int I2cAddress { get; set; } = 0x44; // SHT31 default address
    
    [Range(5, 60)]
    public int ReadingIntervalSeconds { get; set; } = 10;
}

public class UartConfig
{
    [Required]
    public string PortName { get; set; } = "/dev/serial0";
    
    [Range(9600, 115200)]
    public int BaudRate { get; set; } = 115200;
}

public class ThermostatConfig
{
    [Required]
    [StringLength(20)]
    public string TemperatureUnit { get; set; } = "Fahrenheit";
    
    [Range(50, 90)]
    public double DefaultSetpoint { get; set; } = 72.0;
    
    [Range(40, 90)]
    public double MinSetpoint { get; set; } = 60.0;
    
    [Range(50, 95)]
    public double MaxSetpoint { get; set; } = 85.0;
    
    [Range(0.1, 5.0)]
    public double SetpointStep { get; set; } = 1.0;
    
    [Range(0.5, 5.0)]
    public double Hysteresis { get; set; } = 1.0;
    
    [Required]
    [StringLength(20)]
    public string DefaultMode { get; set; } = "Off";
    
    [Range(0, 600)]
    public int CompressorDelaySeconds { get; set; } = 180;
    
    [Range(60, 1800)]
    public int MinRunTimeSeconds { get; set; } = 300;
    
    [Range(600, 7200)]
    public int MaxRunTimeSeconds { get; set; } = 3600;
    
    [Range(5, 60)]
    public int ReadingIntervalSeconds { get; set; } = 10;
}
