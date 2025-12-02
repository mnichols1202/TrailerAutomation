namespace TrailerAutomationGateway
{
    /// <summary>
    /// Request model for device registration.
    /// </summary>
    public class DeviceRegistrationRequest
    {
        public string DeviceId { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public int CommandPort { get; set; }
        public string[] Capabilities { get; set; } = Array.Empty<string>();
    }
}
