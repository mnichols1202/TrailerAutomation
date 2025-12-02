using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TrailerAutomationGateway
{
    /// <summary>
    /// Service for sending TCP commands to client devices.
    /// </summary>
    public class DeviceCommandService
    {
        private readonly DeviceRegistry _deviceRegistry;
        private readonly TimeSpan _connectionTimeout = TimeSpan.FromSeconds(5);
        private readonly TimeSpan _responseTimeout = TimeSpan.FromSeconds(10);

        public DeviceCommandService(DeviceRegistry deviceRegistry)
        {
            _deviceRegistry = deviceRegistry ?? throw new ArgumentNullException(nameof(deviceRegistry));
        }

        /// <summary>
        /// Send a relay control command to a device.
        /// </summary>
        public async Task<CommandResult> SendSetRelayCommandAsync(string deviceId, string relayId, string state)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                return CommandResult.ErrorResult("DeviceId is required", "INVALID_DEVICE_ID");
            }

            if (string.IsNullOrWhiteSpace(relayId))
            {
                return CommandResult.ErrorResult("RelayId is required", "INVALID_RELAY_ID");
            }

            if (string.IsNullOrWhiteSpace(state))
            {
                return CommandResult.ErrorResult("State is required", "INVALID_STATE");
            }

            // Resolve device from registry
            var device = _deviceRegistry.GetDevice(deviceId);
            if (device == null)
            {
                Console.WriteLine($"[DeviceCommand] Device not found: {deviceId}");
                return CommandResult.ErrorResult(
                    $"Device '{deviceId}' not found in registry",
                    "DEVICE_NOT_FOUND");
            }

            Console.WriteLine(
                $"[DeviceCommand] Sending setRelay command to {deviceId} " +
                $"({device.IpAddress}:{device.CommandPort}) " +
                $"RelayId={relayId} State={state}");

            // Generate unique command ID
            var commandId = Guid.NewGuid().ToString();

            // Build command object
            var command = new
            {
                commandId = commandId,
                type = "setRelay",
                payload = new
                {
                    relayId = relayId,
                    state = state
                }
            };

            try
            {
                // Serialize command to JSON
                string commandJson = JsonSerializer.Serialize(command);

                // Open TCP connection
                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(device.IpAddress, device.CommandPort);
                
                if (await Task.WhenAny(connectTask, Task.Delay(_connectionTimeout)) != connectTask)
                {
                    Console.WriteLine($"[DeviceCommand] Connection timeout to {device.IpAddress}:{device.CommandPort}");
                    return CommandResult.ErrorResult(
                        $"Connection timeout to device {deviceId}",
                        "CONNECTION_TIMEOUT");
                }

                await connectTask; // Propagate any exceptions

                Console.WriteLine($"[DeviceCommand] Connected to {device.IpAddress}:{device.CommandPort}");

                using var stream = client.GetStream();
                using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
                using var reader = new StreamReader(stream, Encoding.UTF8);

                // Send command (JSON + newline)
                await writer.WriteLineAsync(commandJson);
                Console.WriteLine($"[DeviceCommand] Sent: {commandJson}");

                // Read response with timeout
                var readTask = reader.ReadLineAsync();
                if (await Task.WhenAny(readTask, Task.Delay(_responseTimeout)) != readTask)
                {
                    Console.WriteLine($"[DeviceCommand] Response timeout from {deviceId}");
                    return CommandResult.ErrorResult(
                        $"Response timeout from device {deviceId}",
                        "RESPONSE_TIMEOUT");
                }

                var responseLine = await readTask;
                if (string.IsNullOrEmpty(responseLine))
                {
                    Console.WriteLine($"[DeviceCommand] Empty response from {deviceId}");
                    return CommandResult.ErrorResult(
                        $"Empty response from device {deviceId}",
                        "EMPTY_RESPONSE");
                }

                Console.WriteLine($"[DeviceCommand] Received: {responseLine}");

                // Parse JSON response
                var responseDoc = JsonDocument.Parse(responseLine);
                var root = responseDoc.RootElement;

                // Extract response fields
                var success = root.TryGetProperty("success", out var successProp) && successProp.GetBoolean();
                var message = root.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : null;
                var errorCode = root.TryGetProperty("errorCode", out var errProp) ? errProp.GetString() : null;

                // Build result
                var result = new CommandResult
                {
                    Success = success,
                    Message = message ?? (success ? "Command executed successfully" : "Command failed"),
                    ErrorCode = errorCode,
                    TimestampUtc = DateTime.UtcNow,
                    Data = root.TryGetProperty("data", out var dataProp) 
                        ? JsonSerializer.Deserialize<object>(dataProp.GetRawText()) 
                        : null
                };

                Console.WriteLine(
                    $"[DeviceCommand] Result: Success={result.Success} Message={result.Message}");

                return result;
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"[DeviceCommand] Socket error: {ex.Message}");
                return CommandResult.ErrorResult(
                    $"Network error: {ex.Message}",
                    "SOCKET_ERROR");
            }
            catch (IOException ex)
            {
                Console.WriteLine($"[DeviceCommand] IO error: {ex.Message}");
                return CommandResult.ErrorResult(
                    $"Communication error: {ex.Message}",
                    "IO_ERROR");
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"[DeviceCommand] JSON parse error: {ex.Message}");
                return CommandResult.ErrorResult(
                    $"Invalid response format: {ex.Message}",
                    "JSON_ERROR");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DeviceCommand] Unexpected error: {ex.Message}");
                return CommandResult.ErrorResult(
                    $"Unexpected error: {ex.Message}",
                    "UNKNOWN_ERROR");
            }
        }
    }
}
