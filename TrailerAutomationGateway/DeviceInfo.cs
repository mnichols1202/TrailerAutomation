namespace TrailerAutomationGateway
{
    /// <summary>
    /// Represents detailed device information for command routing.
    /// </summary>
    public class DeviceInfo
    {
        public string DeviceId { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public int CommandPort { get; set; }
        public string[] Capabilities { get; set; } = Array.Empty<string>();
        public DateTime LastSeen { get; set; }

        public DeviceInfo Clone()
        {
            return new DeviceInfo
            {
                DeviceId = DeviceId,
                IpAddress = IpAddress,
                CommandPort = CommandPort,
                Capabilities = (string[])Capabilities.Clone(),
                LastSeen = LastSeen
            };
        }
    }
}
