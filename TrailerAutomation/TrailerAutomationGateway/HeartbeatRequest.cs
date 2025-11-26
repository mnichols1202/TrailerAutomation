namespace TrailerAutomationGateway
{
    /// <summary>
    /// Payload sent by a client to identify itself in a heartbeat.
    /// </summary>
    public sealed class HeartbeatRequest
    {
        public string ClientId { get; set; } = string.Empty;
        public string? DeviceType { get; set; }
        public string? FriendlyName { get; set; }
    }
}
