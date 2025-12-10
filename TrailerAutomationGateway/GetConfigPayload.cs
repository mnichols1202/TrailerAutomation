namespace TrailerAutomationGateway
{
    /// <summary>
    /// Response payload for getConfig command from device.
    /// </summary>
    public class GetConfigPayload
    {
        public string ConfigJson { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request payload for setConfig command to device.
    /// </summary>
    public class SetConfigPayload
    {
        public string ConfigJson { get; set; } = string.Empty;
    }
}
