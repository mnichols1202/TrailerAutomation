using System;

namespace TrailerAutomationGateway
{
    /// <summary>
    /// Represents the latest sensor reading from a client.
    /// </summary>
    public sealed class SensorReading
    {
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// Temperature in degrees Celsius.
        /// </summary>
        public double TemperatureC { get; set; }

        /// <summary>
        /// Relative humidity (0–100).
        /// </summary>
        public double HumidityPercent { get; set; }

        /// <summary>
        /// When this reading was recorded on the gateway (UTC).
        /// </summary>
        public DateTime TimestampUtc { get; set; }

        /// <summary>
        /// Optional: last known remote IP, if we want to keep it.
        /// </summary>
        public string? RemoteIp { get; set; }
    }
}
