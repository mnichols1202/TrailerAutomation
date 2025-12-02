namespace TrailerAutomationGateway
{
    /// <summary>
    /// Result of a command sent to a device.
    /// </summary>
    public class CommandResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? ErrorCode { get; set; }
        public DateTime TimestampUtc { get; set; }
        public object? Data { get; set; }

        public static CommandResult SuccessResult(string? message = null, object? data = null)
        {
            return new CommandResult
            {
                Success = true,
                Message = message ?? "Command executed successfully",
                TimestampUtc = DateTime.UtcNow,
                Data = data
            };
        }

        public static CommandResult ErrorResult(string message, string? errorCode = null)
        {
            return new CommandResult
            {
                Success = false,
                Message = message,
                ErrorCode = errorCode ?? "COMMAND_ERROR",
                TimestampUtc = DateTime.UtcNow
            };
        }
    }
}
