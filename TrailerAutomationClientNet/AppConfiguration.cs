using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TrailerAutomationClientNet
{
    /// <summary>
    /// Root configuration model for the TrailerAutomation client.
    /// </summary>
    public class AppConfiguration
    {
        public DeviceConfig Device { get; set; } = new();
        public IntervalsConfig Intervals { get; set; } = new();
        public GatewayConfig Gateway { get; set; } = new();
        public HardwareConfig Hardware { get; set; } = new();
    }

    public class DeviceConfig
    {
        [Required(ErrorMessage = "ClientId is required")]
        [StringLength(50, MinimumLength = 1, ErrorMessage = "ClientId must be 1-50 characters")]
        public string ClientId { get; set; } = Environment.MachineName;
        
        [Required]
        [StringLength(50)]
        public string DeviceType { get; set; } = "Unknown";
        
        [Required]
        [StringLength(100)]
        public string FriendlyName { get; set; } = "Unnamed Device";
        
        [Range(1, 65535, ErrorMessage = "CommandListenerPort must be between 1 and 65535")]
        public int CommandListenerPort { get; set; } = 8888;
    }

    public class IntervalsConfig
    {
        [Range(5, 600, ErrorMessage = "HeartbeatSeconds must be between 5 and 600")]
        public int HeartbeatSeconds { get; set; } = 60;
        
        // SensorReadingSeconds removed - now configured per-sensor in Hardware.Sensors[].ReadingIntervalSeconds
        
        [Range(1, 60, ErrorMessage = "CommandPollingSeconds must be between 1 and 60")]
        public int CommandPollingSeconds { get; set; } = 3;
    }

    public class GatewayConfig
    {
        public int DiscoveryTimeoutSeconds { get; set; } = 8;
    }

    public class HardwareConfig
    {
        public List<RelayConfig> Relays { get; set; } = new();
        public List<SensorConfig> Sensors { get; set; } = new();
        public List<ButtonConfig> Buttons { get; set; } = new();
    }

    public class RelayConfig
    {
        [Required]
        [StringLength(50)]
        public string Id { get; set; } = string.Empty;
        
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [Range(0, 100, ErrorMessage = "Pin must be a valid GPIO pin number")]
        public int Pin { get; set; }
        
        public bool InitialState { get; set; } = false;
    }

    public class SensorConfig
    {
        [Required]
        [StringLength(50)]
        public string Id { get; set; } = string.Empty;
        
        [Required]
        [StringLength(50)]
        public string Type { get; set; } = string.Empty;
        
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
        
        public string I2cAddress { get; set; } = string.Empty;
        
        public bool Enabled { get; set; } = true;
        
        [Range(10, 3600, ErrorMessage = "ReadingIntervalSeconds must be between 10 and 3600")]
        public int ReadingIntervalSeconds { get; set; } = 300; // Default 5 minutes for slow-changing sensors
    }

    public class ButtonConfig
    {
        [Required]
        [StringLength(50)]
        public string Id { get; set; } = string.Empty;
        
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [Range(0, 100, ErrorMessage = "Pin must be a valid GPIO pin number")]
        public int Pin { get; set; }
        
        [Required]
        [StringLength(50)]
        public string TargetDevice { get; set; } = string.Empty;
        
        [Required]
        [StringLength(50)]
        public string TargetRelay { get; set; } = string.Empty;
        
        public bool Enabled { get; set; } = true;
    }
}
