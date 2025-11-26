namespace TrailerAutomationGateway
{
    public class ClientInfo
    {
        public string ClientId { get; set; } = string.Empty;
        public string? DeviceType { get; set; }
        public string? FriendlyName { get; set; }
        public string? RemoteIp { get; set; }
        public DateTime FirstSeenUtc { get; set; }
        public DateTime LastHeartbeatUtc { get; set; }

        public ClientInfo Clone()
        {
            return new ClientInfo
            {
                ClientId = ClientId,
                DeviceType = DeviceType,
                FriendlyName = FriendlyName,
                RemoteIp = RemoteIp,
                FirstSeenUtc = FirstSeenUtc,
                LastHeartbeatUtc = LastHeartbeatUtc
            };
        }
    }
}
