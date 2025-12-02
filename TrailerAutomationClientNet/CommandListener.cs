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
        private TcpListener? _listener;
        private CancellationTokenSource? _cts;

        public CommandListener(AppConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Start the TCP listener in the background.
        /// </summary>
        public void Start()
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            Task.Run(async () => await RunListenerAsync(token), token);
        }

        /// <summary>
        /// Stop the TCP listener.
        /// </summary>
        public void Stop()
        {
            _cts?.Cancel();
            _listener?.Stop();
        }

        private async Task RunListenerAsync(CancellationToken cancellationToken)
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

                    if (commandType == "setRelay")
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

                // TODO: Actual GPIO control would go here
                // For now, simulate the operation
                await Task.Delay(50); // Simulate relay operation time

                return new
                {
                    commandId = commandId,
                    success = true,
                    message = $"Relay '{relayId}' set to {state}",
                    data = new
                    {
                        relayId = relayId,
                        state = state,
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
