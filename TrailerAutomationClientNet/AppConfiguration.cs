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
        [Range(5, 300, ErrorMessage = "HeartbeatSeconds must be between 5 and 300")]
        public int HeartbeatSeconds { get; set; } = 10;
        
        [Range(10, 3600, ErrorMessage = "SensorReadingSeconds must be between 10 and 3600")]
        public int SensorReadingSeconds { get; set; } = 30;
        
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
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string I2cAddress { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
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
