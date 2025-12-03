using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TrailerAutomationClientNet
{
    /// <summary>
    /// TCP listener service that receives commands from the gateway.
    /// </summary>
    public class CommandListener
    {
        private readonly AppConfiguration _config;
        private readonly GpioRelayController _gpioController;
        private TcpListener? _listener;

        public CommandListener(AppConfiguration config, GpioRelayController gpioController)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _gpioController = gpioController ?? throw new ArgumentNullException(nameof(gpioController));
        }

        /// <summary>
        /// Run the TCP listener (async cancellable).
        /// </summary>
        public async Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                _listener = new TcpListener(IPAddress.Any, _config.Device.CommandListenerPort);
                _listener.Start();

                Console.WriteLine($"[CommandListener] Listening on port {_config.Device.CommandListenerPort}");

                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // Accept incoming connection
                        var client = await _listener.AcceptTcpClientAsync();
                        var clientEndpoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";

                        Console.WriteLine($"[CommandListener] Connection from {clientEndpoint}");

                        // Handle connection in background to continue accepting new connections
                        _ = Task.Run(async () => await HandleClientAsync(client, clientEndpoint), cancellationToken);
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        Console.WriteLine($"[CommandListener] Accept error: {ex.Message}");
                        await Task.Delay(1000, cancellationToken); // Brief delay before retry
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CommandListener] Fatal error: {ex.Message}");
            }
            finally
            {
                _listener?.Stop();
                Console.WriteLine("[CommandListener] Stopped");
            }
        }

        private async Task HandleClientAsync(TcpClient client, string clientEndpoint)
        {
            try
            {
                using (client)
                using (var stream = client.GetStream())
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                using (var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true })
                {
                    // Read one line of JSON
                    var commandLine = await reader.ReadLineAsync();

                    if (string.IsNullOrEmpty(commandLine))
                    {
                        Console.WriteLine($"[CommandListener] Empty command from {clientEndpoint}");
                        return;
                    }

                    Console.WriteLine($"[CommandListener] Received: {commandLine}");

                    // Parse command JSON
                    var commandDoc = JsonDocument.Parse(commandLine);
                    var root = commandDoc.RootElement;

                    var commandId = root.TryGetProperty("commandId", out var cmdIdProp) 
                        ? cmdIdProp.GetString() 
                        : "unknown";
                    var commandType = root.TryGetProperty("type", out var typeProp) 
                        ? typeProp.GetString() 
                        : null;

                    // Handle command based on type
                    object? response = null;

                    if (commandType == "ping")
                    {
                        // Simple ping/pong response for connectivity check
                        response = new
                        {
                            commandId = commandId,
                            success = true,
                            message = "pong"
                        };
                        Console.WriteLine($"[CommandListener] Ping received, responding with pong");
                    }
                    else if (commandType == "identify")
                    {
                        // Return actual ClientId so gateway can verify identity
                        response = new
                        {
                            commandId = commandId,
                            success = true,
                            message = "Device identified",
                            data = new
                            {
                                clientId = _config.Device.ClientId,
                                deviceType = _config.Device.DeviceType,
                                friendlyName = _config.Device.FriendlyName
                            }
                        };
                        Console.WriteLine($"[CommandListener] Identify command received, responding with ClientId: {_config.Device.ClientId}");
                    }
                    else if (commandType == "setRelay")
                    {
                        response = await HandleSetRelayCommandAsync(root, commandId);
                    }
                    else
                    {
                        response = new
                        {
                            commandId = commandId,
                            success = false,
                            message = $"Unknown command type: {commandType}",
                            errorCode = "UNKNOWN_COMMAND"
                        };
                        Console.WriteLine($"[CommandListener] Unknown command type: {commandType}");
                    }

                    // Serialize and send response
                    string responseJson = JsonSerializer.Serialize(response);
                    await writer.WriteLineAsync(responseJson);
                    await writer.FlushAsync();

                    Console.WriteLine($"[CommandListener] Sent response: {responseJson}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CommandListener] Error handling client {clientEndpoint}: {ex.Message}");
            }
        }

        private async Task<object> HandleSetRelayCommandAsync(JsonElement command, string? commandId)
        {
            try
            {
                // Extract payload
                if (!command.TryGetProperty("payload", out var payload))
                {
                    return new
                    {
                        commandId = commandId,
                        success = false,
                        message = "Missing payload",
                        errorCode = "MISSING_PAYLOAD"
                    };
                }

                var relayId = payload.TryGetProperty("relayId", out var relayIdProp) 
                    ? relayIdProp.GetString() 
                    : null;
                var state = payload.TryGetProperty("state", out var stateProp) 
                    ? stateProp.GetString() 
                    : null;

                if (string.IsNullOrEmpty(relayId) || string.IsNullOrEmpty(state))
                {
                    return new
                    {
                        commandId = commandId,
                        success = false,
                        message = "Invalid payload: relayId and state required",
                        errorCode = "INVALID_PAYLOAD"
                    };
                }

                // Find relay in configuration
                var relay = _config.Hardware.Relays.Find(r => r.Id == relayId);
                if (relay == null)
                {
                    return new
                    {
                        commandId = commandId,
                        success = false,
                        message = $"Relay '{relayId}' not found",
                        errorCode = "RELAY_NOT_FOUND"
                    };
                }

                Console.WriteLine($"[CommandListener] SetRelay: {relayId} -> {state} (Pin: {relay.Pin})");

                // Control GPIO pin
                bool success = _gpioController.SetRelay(relayId, state);

                if (!success)
                {
                    return new
                    {
                        commandId = commandId,
                        success = false,
                        message = $"Failed to set relay '{relayId}' to {state}",
                        errorCode = "GPIO_ERROR"
                    };
                }

                // Get current state after setting
                var currentState = _gpioController.GetRelayState(relayId);

                return new
                {
                    commandId = commandId,
                    success = true,
                    message = $"Relay '{relayId}' set to {state}",
                    data = new
                    {
                        relayId = relayId,
                        state = currentState ?? state,
                        pin = relay.Pin
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CommandListener] SetRelay error: {ex.Message}");
                return new
                {
                    commandId = commandId,
                    success = false,
                    message = $"Error: {ex.Message}",
                    errorCode = "EXECUTION_ERROR"
                };
            }
        }
    }
}
