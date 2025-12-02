using System.Text.Json;

namespace TrailerAutomationGateway
{
    /// <summary>
    /// Generic device command model.
    /// </summary>
    public class DeviceCommand
    {
        public string CommandId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public JsonElement Payload { get; set; }
    }

    /// <summary>
    /// Strongly-typed payload for setRelay command.
    /// </summary>
    public class SetRelayPayload
    {
        public string RelayId { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
    }
}
