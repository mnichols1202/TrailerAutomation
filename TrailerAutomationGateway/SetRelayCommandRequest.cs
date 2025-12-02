namespace TrailerAutomationGateway
{
    /// <summary>
    /// Request model for setting relay state.
    /// </summary>
    public class SetRelayCommandRequest
    {
        public string RelayId { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
    }
}
