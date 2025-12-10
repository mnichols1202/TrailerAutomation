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
        private readonly ButtonController? _buttonController;
        private TcpListener? _listener;

        public CommandListener(AppConfiguration config, GpioRelayController gpioController, ButtonController? buttonController = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _gpioController = gpioController ?? throw new ArgumentNullException(nameof(gpioController));
            _buttonController = buttonController;
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
                    else if (commandType == "init")
                    {
                        // Trigger re-initialization by calling RegisterDeviceAsync
                        response = new
                        {
                            commandId = commandId,
                            success = true,
                            message = "Device will re-initialize"
                        };
                        Console.WriteLine($"[CommandListener] Init command received, will trigger re-registration");
                        
                        // Send response first
                        string initResponseJson = JsonSerializer.Serialize(response);
                        await writer.WriteLineAsync(initResponseJson);
                        await writer.FlushAsync();
                        Console.WriteLine($"[CommandListener] Sent response: {initResponseJson}");
                        
                        // Trigger re-registration in background
                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(500);  // Brief delay
                            await Program.TriggerReRegistrationAsync();
                        });
                        
                        return; // Skip normal response sending
                    }
                    else if (commandType == "reboot")
                    {
                        // Reboot the application
                        response = new
                        {
                            commandId = commandId,
                            success = true,
                            message = "Device rebooting"
                        };
                        Console.WriteLine($"[CommandListener] Reboot command received, application will restart");
                        
                        // Send response first
                        string rebootResponseJson = JsonSerializer.Serialize(response);
                        await writer.WriteLineAsync(rebootResponseJson);
                        await writer.FlushAsync();
                        Console.WriteLine($"[CommandListener] Sent response: {rebootResponseJson}");
                        
                        // Exit application (systemd or container orchestrator should restart it)
                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(1000);
                            Environment.Exit(0);
                        });
                        
                        return; // Skip normal response sending
                    }
                    else if (commandType == "setRelay")
                    {
                        response = await HandleSetRelayCommandAsync(root, commandId);
                    }
                    else if (commandType == "getRelayState")
                    {
                        response = HandleGetRelayStateCommand(root, commandId);
                    }
                    else if (commandType == "getConfig")
                    {
                        response = HandleGetConfigCommand(commandId);
                    }
                    else if (commandType == "getConfigRaw")
                    {
                        response = HandleGetConfigRawCommand(commandId);
                    }
                    else if (commandType == "setConfig")
                    {
                        response = HandleSetConfigCommand(root, commandId);
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

                // Sync button state tracking
                if (success)
                {
                    _buttonController?.SyncRelayState(relayId, state);
                }

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

        private object HandleGetRelayStateCommand(JsonElement command, string? commandId)
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

                if (string.IsNullOrEmpty(relayId))
                {
                    return new
                    {
                        commandId = commandId,
                        success = false,
                        message = "Invalid payload: relayId required",
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

                // Get current state from GPIO controller
                var currentState = _gpioController.GetRelayState(relayId);

                Console.WriteLine($"[CommandListener] GetRelayState: {relayId} -> {currentState} (Pin: {relay.Pin})");

                return new
                {
                    commandId = commandId,
                    success = true,
                    message = $"Relay '{relayId}' state retrieved",
                    data = new
                    {
                        relayId = relayId,
                        state = currentState ?? "unknown",
                        pin = relay.Pin
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CommandListener] GetRelayState error: {ex.Message}");
                return new
                {
                    commandId = commandId,
                    success = false,
                    message = $"Error: {ex.Message}",
                    errorCode = "EXECUTION_ERROR"
                };
            }
        }

        private object HandleGetConfigCommand(string? commandId)
        {
            try
            {
                Console.WriteLine($"[CommandListener] GetConfig command received");

                // Build configuration response
                var configData = new
                {
                    device = new
                    {
                        clientId = _config.Device.ClientId,
                        deviceType = _config.Device.DeviceType,
                        friendlyName = _config.Device.FriendlyName,
                        commandListenerPort = _config.Device.CommandListenerPort
                    },
                    intervals = new
                    {
                        heartbeatSeconds = _config.Intervals.HeartbeatSeconds,
                        commandPollingSeconds = _config.Intervals.CommandPollingSeconds
                    },
                    gateway = new
                    {
                        discoveryTimeoutSeconds = _config.Gateway.DiscoveryTimeoutSeconds
                    },
                    relays = _config.Hardware.Relays.Select(r => new
                    {
                        id = r.Id,
                        name = r.Name,
                        pin = r.Pin,
                        initialState = r.InitialState
                    }).ToList(),
                    sensors = _config.Hardware.Sensors.Select(s => new
                    {
                        id = s.Id,
                        type = s.Type,
                        name = s.Name,
                        i2cAddress = s.I2cAddress,
                        enabled = s.Enabled,
                        readingIntervalSeconds = s.ReadingIntervalSeconds
                    }).ToList(),
                    buttons = _buttonController != null 
                        ? _config.Hardware.Buttons.Select(b => new
                        {
                            id = b.Id,
                            name = b.Name,
                            pin = b.Pin,
                            targetDevice = b.TargetDevice,
                            targetRelay = b.TargetRelay,
                            enabled = b.Enabled
                        }).Cast<object>().ToList()
                        : new List<object>()
                };

                return new
                {
                    commandId = commandId,
                    success = true,
                    message = "Configuration retrieved",
                    data = configData
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CommandListener] GetConfig error: {ex.Message}");
                return new
                {
                    commandId = commandId,
                    success = false,
                    message = $"Error: {ex.Message}",
                    errorCode = "EXECUTION_ERROR"
                };
            }
        }

        private object HandleGetConfigRawCommand(string? commandId)
        {
            try
            {
                Console.WriteLine($"[CommandListener] GetConfigRaw command received");

                // Read raw config.json file
                string configPath = "config.json";
                
                if (!File.Exists(configPath))
                {
                    return new
                    {
                        commandId = commandId,
                        success = false,
                        message = "config.json file not found",
                        errorCode = "FILE_NOT_FOUND"
                    };
                }

                string configJson = File.ReadAllText(configPath);

                return new
                {
                    commandId = commandId,
                    success = true,
                    message = "Configuration file retrieved",
                    data = new
                    {
                        configJson = configJson
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CommandListener] GetConfigRaw error: {ex.Message}");
                return new
                {
                    commandId = commandId,
                    success = false,
                    message = $"Error: {ex.Message}",
                    errorCode = "EXECUTION_ERROR"
                };
            }
        }

        private object HandleSetConfigCommand(JsonElement root, string? commandId)
        {
            try
            {
                Console.WriteLine($"[CommandListener] SetConfig command received");

                // Extract payload
                if (!root.TryGetProperty("payload", out JsonElement payload))
                {
                    return new
                    {
                        commandId = commandId,
                        success = false,
                        message = "Missing payload",
                        errorCode = "MISSING_PAYLOAD"
                    };
                }

                if (!payload.TryGetProperty("configJson", out JsonElement configJsonElement))
                {
                    return new
                    {
                        commandId = commandId,
                        success = false,
                        message = "Invalid payload: configJson required",
                        errorCode = "INVALID_PAYLOAD"
                    };
                }

                string configJson = configJsonElement.GetString() ?? "";

                // Validate JSON before writing
                try
                {
                    JsonDocument.Parse(configJson);
                }
                catch (JsonException ex)
                {
                    return new
                    {
                        commandId = commandId,
                        success = false,
                        message = $"Invalid JSON: {ex.Message}",
                        errorCode = "INVALID_JSON"
                    };
                }

                // Write to file
                string configPath = "config.json";
                File.WriteAllText(configPath, configJson);

                Console.WriteLine($"[CommandListener] SetConfig wrote {configJson.Length} bytes to config.json");

                return new
                {
                    commandId = commandId,
                    success = true,
                    message = $"Configuration updated ({configJson.Length} bytes). Device will reload config on next restart.",
                    data = new { }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CommandListener] SetConfig error: {ex.Message}");
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

