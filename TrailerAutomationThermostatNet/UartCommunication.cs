using System.IO.Ports;
using System.Text;

namespace TrailerAutomationThermostatNet;

/// <summary>
/// UART communication with ESP32-S3 relay box.
/// Simple text protocol for thermostat control commands.
/// </summary>
public class UartCommunication : IDisposable
{
    private readonly SerialPort _serialPort;
    private readonly CancellationTokenSource _cts = new();
    private Task? _receiveTask;
    private bool _disposed;

    public event EventHandler<string>? MessageReceived;
    public event EventHandler<SystemStatus>? StatusChanged;

    public UartCommunication(UartConfig config)
    {
        _serialPort = new SerialPort(config.PortName, config.BaudRate)
        {
            Parity = Parity.None,
            DataBits = 8,
            StopBits = StopBits.One,
            Handshake = Handshake.None,
            ReadTimeout = 1000,
            WriteTimeout = 1000
        };

        Console.WriteLine($"[UART] Configured {config.PortName} @ {config.BaudRate} baud");
    }

    public void Start()
    {
        try
        {
            _serialPort.Open();
            Console.WriteLine("[UART] Port opened successfully");

            _receiveTask = Task.Run(() => ReceiveLoop(_cts.Token), _cts.Token);
            Console.WriteLine("[UART] Receive loop started");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UART] ERROR: Failed to open port: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Send current temperature to relay box.
    /// Format: "TEMP:72.5\n"
    /// </summary>
    public void SendTemperature(double temperature)
    {
        SendMessage($"TEMP:{temperature:F1}");
    }

    /// <summary>
    /// Send setpoint change to relay box.
    /// Format: "SET:68\n"
    /// </summary>
    public void SendSetpoint(double setpoint)
    {
        SendMessage($"SET:{setpoint:F0}");
    }

    /// <summary>
    /// Send mode change to relay box.
    /// Format: "MODE:COOL\n" or "MODE:HEAT\n" or "MODE:AUTO\n" or "MODE:OFF\n"
    /// </summary>
    public void SendMode(ThermostatMode mode)
    {
        SendMessage($"MODE:{mode.ToString().ToUpper()}");
    }

    /// <summary>
    /// Request status from relay box.
    /// Format: "STATUS?\n"
    /// </summary>
    public void RequestStatus()
    {
        SendMessage("STATUS?");
    }

    private void SendMessage(string message)
    {
        if (!_serialPort.IsOpen)
        {
            Console.WriteLine("[UART] WARNING: Port not open, cannot send message");
            return;
        }

        try
        {
            var data = Encoding.UTF8.GetBytes(message + "\n");
            _serialPort.Write(data, 0, data.Length);
            Console.WriteLine($"[UART] TX: {message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UART] ERROR: Failed to send message: {ex.Message}");
        }
    }

    private async Task ReceiveLoop(CancellationToken cancellationToken)
    {
        var buffer = new StringBuilder();

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_serialPort.BytesToRead > 0)
                {
                    var data = _serialPort.ReadExisting();
                    buffer.Append(data);

                    // Process complete lines (terminated by \n)
                    var text = buffer.ToString();
                    var lines = text.Split('\n');

                    // Process all complete lines
                    for (int i = 0; i < lines.Length - 1; i++)
                    {
                        var line = lines[i].Trim();
                        if (!string.IsNullOrEmpty(line))
                        {
                            ProcessMessage(line);
                        }
                    }

                    // Keep incomplete line in buffer
                    buffer.Clear();
                    if (lines.Length > 0)
                    {
                        buffer.Append(lines[^1]);
                    }
                }

                await Task.Delay(50, cancellationToken); // Poll every 50ms
            }
            catch (TimeoutException)
            {
                // Normal timeout, continue
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Console.WriteLine($"[UART] ERROR: Receive error: {ex.Message}");
                await Task.Delay(1000, cancellationToken);
            }
        }
    }

    private void ProcessMessage(string message)
    {
        Console.WriteLine($"[UART] RX: {message}");

        // Parse messages from relay box
        // Expected formats:
        // "STATUS:COOLING" - System is cooling
        // "STATUS:HEATING" - System is heating
        // "STATUS:IDLE" - System is idle
        // "STATUS:WAITING" - Waiting for compressor delay
        // "RELAY:AC_COMP:ON" - Relay state notification
        // "ACK" - Command acknowledged

        MessageReceived?.Invoke(this, message);

        if (message.StartsWith("STATUS:"))
        {
            var statusStr = message.Substring(7);
            if (Enum.TryParse<SystemStatus>(statusStr, true, out var status))
            {
                StatusChanged?.Invoke(this, status);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _cts?.Cancel();
        _receiveTask?.Wait(TimeSpan.FromSeconds(2));

        if (_serialPort?.IsOpen == true)
        {
            _serialPort.Close();
        }

        _serialPort?.Dispose();
        _cts?.Dispose();

        _disposed = true;
        Console.WriteLine("[UART] Disposed");
    }
}
