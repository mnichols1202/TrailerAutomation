using System;
using System.Collections.Generic;

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
        public string ClientId { get; set; } = Environment.MachineName;
        public string DeviceType { get; set; } = "Unknown";
        public string FriendlyName { get; set; } = "Unnamed Device";
        public int CommandListenerPort { get; set; } = 8888;
    }

    public class IntervalsConfig
    {
        public int HeartbeatSeconds { get; set; } = 10;
        public int SensorReadingSeconds { get; set; } = 30;
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
    }

    public class RelayConfig
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
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
}
