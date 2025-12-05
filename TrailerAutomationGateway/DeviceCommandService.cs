using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TrailerAutomationGateway
{
    /// <summary>
    /// Service for sending TCP commands to client devices using line-delimited JSON protocol.
    /// </summary>
    public class DeviceCommandService
    {
        private readonly ClientRegistry _clientRegistry;
        private const int ConnectionTimeoutMs = 5000;
        private const int ResponseTimeoutMs = 5000;

        public DeviceCommandService(ClientRegistry clientRegistry)
        {
            _clientRegistry = clientRegistry ?? throw new ArgumentNullException(nameof(clientRegistry));
        }

        /// <summary>
        /// Send a generic command to a device.
        /// </summary>
        public async Task<CommandResult> SendCommandAsync(string clientId, DeviceCommand command)
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                return CommandResult.ErrorResult("ClientId is required", "INVALID_CLIENT_ID");
            }

            if (command == null)
            {
                return CommandResult.ErrorResult("Command is required", "INVALID_COMMAND");
            }

            // Resolve device from registry
            var client = _clientRegistry.GetClient(clientId);
            if (client == null)
            {
                Console.WriteLine($"[DeviceCommand] Device not found: {clientId}");
                return CommandResult.ErrorResult(
                    $"Device '{clientId}' not found in registry",
                    "DEVICE_NOT_FOUND");
            }

            if (string.IsNullOrWhiteSpace(client.RemoteIp))
            {
                Console.WriteLine($"[DeviceCommand] Device has no IP address: {clientId}");
                return CommandResult.ErrorResult(
                    $"Device '{clientId}' has no IP address",
                    "NO_IP_ADDRESS");
            }

            if (client.CommandPort <= 0)
            {
                Console.WriteLine($"[DeviceCommand] Device has no command port: {clientId}");
                return CommandResult.ErrorResult(
                    $"Device '{clientId}' has no command port configured",
                    "NO_COMMAND_PORT");
            }

            Console.WriteLine(
                $"[DeviceCommand] Sending {command.Type} command to {clientId} " +
                $"({client.RemoteIp}:{client.CommandPort}) " +
                $"CommandId={command.CommandId}");

            try
            {
                // Serialize command to JSON
                string commandJson = JsonSerializer.Serialize(command, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                // Open TCP connection with timeout
                using var tcpClient = new TcpClient();
                tcpClient.SendTimeout = ConnectionTimeoutMs;
                tcpClient.ReceiveTimeout = ResponseTimeoutMs;

                var connectTask = tcpClient.ConnectAsync(client.RemoteIp, client.CommandPort);
                
                if (await Task.WhenAny(connectTask, Task.Delay(ConnectionTimeoutMs)).ConfigureAwait(false) != connectTask)
                {
                    Console.WriteLine($"[DeviceCommand] Connection timeout to {client.RemoteIp}:{client.CommandPort}");
                    return CommandResult.ErrorResult(
                        $"Connection timeout to device {clientId}",
                        "CONNECTION_TIMEOUT");
                }

                await connectTask.ConfigureAwait(false); // Propagate any exceptions

                Console.WriteLine($"[DeviceCommand] Connected to {client.RemoteIp}:{client.CommandPort}");

                await using var stream = tcpClient.GetStream();
                // Use UTF8 without BOM to avoid parse errors on ESP32
                var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
                await using var writer = new StreamWriter(stream, utf8NoBom) { AutoFlush = true };
                using var reader = new StreamReader(stream, utf8NoBom);

                // Send command (JSON + newline)
                await writer.WriteLineAsync(commandJson);
                await writer.FlushAsync();
                Console.WriteLine($"[DeviceCommand] Sent: {commandJson}");

                // Read response with timeout
                var readTask = reader.ReadLineAsync();
                if (await Task.WhenAny(readTask, Task.Delay(ResponseTimeoutMs)) != readTask)
                {
                    Console.WriteLine($"[DeviceCommand] Response timeout from {clientId}");
                    return CommandResult.ErrorResult(
                        $"Response timeout from device {clientId}",
                        "RESPONSE_TIMEOUT");
                }

                var responseLine = await readTask;
                if (string.IsNullOrEmpty(responseLine))
                {
                    Console.WriteLine($"[DeviceCommand] Empty response from {clientId}");
                    return CommandResult.ErrorResult(
                        $"Empty response from device {clientId}",
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

        /// <summary>
        /// Convenience method to send a relay control command.
        /// </summary>
        public async Task<CommandResult> SendSetRelayCommandAsync(string clientId, string relayId, string state)
        {
            if (string.IsNullOrWhiteSpace(relayId))
            {
                return CommandResult.ErrorResult("RelayId is required", "INVALID_RELAY_ID");
            }

            if (string.IsNullOrWhiteSpace(state))
            {
                return CommandResult.ErrorResult("State is required", "INVALID_STATE");
            }

            // Build command using generic model
            var command = new DeviceCommand
            {
                CommandId = Guid.NewGuid().ToString(),
                Type = "setRelay",
                Payload = JsonSerializer.SerializeToElement(new SetRelayPayload
                {
                    RelayId = relayId,
                    State = state
                }, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                })
            };

            return await SendCommandAsync(clientId, command);
        }
    }
}
