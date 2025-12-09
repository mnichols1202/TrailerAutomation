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
        
        /// <summary>
        /// Current relay states (relayId -> "on"/"off"). Used for state sync after Gateway restart.
        /// </summary>
        public Dictionary<string, string>? RelayStates { get; set; }
    }
}
