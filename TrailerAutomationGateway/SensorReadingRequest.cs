namespace TrailerAutomationGateway
{
    /// <summary>
    /// Request payload for posting a single temperature / humidity reading from a client.
    /// </summary>
    public sealed class SensorReadingRequest
    {
        /// <summary>
        /// Logical identifier for the client sending the reading (for example, the device hostname).
        /// </summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// Temperature in degrees Celsius as measured by the client.
        /// </summary>
        public double TemperatureC { get; set; }

        /// <summary>
        /// Relative humidity (0–100%) as measured by the client.
        /// </summary>
        public double HumidityPercent { get; set; }
    }
}
