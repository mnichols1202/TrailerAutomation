namespace TrailerAutomationGateway
{
    /// <summary>
    /// Unified client/device information model containing all fields for heartbeat, sensor, and command routing.
    /// </summary>
    public class ClientInfo
    {
        public string ClientId { get; set; } = string.Empty;
        public string? DeviceType { get; set; }
        public string? FriendlyName { get; set; }
        public string? RemoteIp { get; set; }
        public DateTime FirstSeenUtc { get; set; }
        public DateTime LastHeartbeatUtc { get; set; }
        
        // Command routing fields
        public int CommandPort { get; set; }
        public string[] Capabilities { get; set; } = Array.Empty<string>();
        public RelayInfo[]? Relays { get; set; }

        public ClientInfo Clone()
        {
            return new ClientInfo
            {
                ClientId = ClientId,
                DeviceType = DeviceType,
                FriendlyName = FriendlyName,
                RemoteIp = RemoteIp,
                FirstSeenUtc = FirstSeenUtc,
                LastHeartbeatUtc = LastHeartbeatUtc,
                CommandPort = CommandPort,
                Capabilities = (string[])Capabilities.Clone(),
                Relays = Relays != null ? (RelayInfo[])Relays.Clone() : null
            };
        }
    }
}
