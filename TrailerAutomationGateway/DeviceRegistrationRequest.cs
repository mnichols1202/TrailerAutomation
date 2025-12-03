namespace TrailerAutomationGateway
{
    /// <summary>
    /// Request model for device registration.
    /// </summary>
    public class DeviceRegistrationRequest
    {
        public string ClientId { get; set; } = string.Empty;
        public string? DeviceType { get; set; }
        public string? FriendlyName { get; set; }
        public string IpAddress { get; set; } = string.Empty;
        public int CommandPort { get; set; }
        public string[] Capabilities { get; set; } = Array.Empty<string>();
        public RelayInfo[]? Relays { get; set; }
    }

    public class RelayInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
