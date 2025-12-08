namespace TrailerAutomationThermostatNet;

/// <summary>
/// Core thermostat control logic.
/// Manages temperature control, mode switching, and safety interlocks.
/// </summary>
public class ThermostatController
{
    private readonly ThermostatConfig _config;
    private readonly Sht31Reader _sensor;
    private readonly UartCommunication _uart;
    private readonly DisplayController _display;
    
    private ThermostatMode _currentMode;
    private double _setpoint;
    private double _currentTemperature;
    private double _currentHumidity;
    private SystemStatus _systemStatus = SystemStatus.Idle;
    private DateTime? _lastCompressorStop;
    private DateTime? _systemRunStart;
    
    private readonly CancellationTokenSource _cts = new();
    private Task? _controlLoopTask;
    private Task? _sensorReadTask;
    
    private bool _useFahrenheit;

    public ThermostatController(
        ThermostatConfig config,
        Sht31Reader sensor,
        UartCommunication uart,
        DisplayController display)
    {
        _config = config;
        _sensor = sensor;
        _uart = uart;
        _display = display;
        
        _useFahrenheit = config.TemperatureUnit.Equals("Fahrenheit", StringComparison.OrdinalIgnoreCase);
        _setpoint = config.DefaultSetpoint;
        
        // Parse default mode
        if (!Enum.TryParse<ThermostatMode>(config.DefaultMode, true, out _currentMode))
        {
            _currentMode = ThermostatMode.Off;
        }
        
        // Subscribe to UART status updates
        _uart.StatusChanged += OnRelayBoxStatusChanged;
        
        Console.WriteLine($"[Thermostat] Initialized - Mode: {_currentMode}, Setpoint: {_setpoint}°{(_useFahrenheit ? "F" : "C")}");
    }

    public void Start()
    {
        _sensorReadTask = Task.Run(() => SensorReadLoop(_cts.Token), _cts.Token);
        _controlLoopTask = Task.Run(() => ControlLoop(_cts.Token), _cts.Token);
        Console.WriteLine("[Thermostat] Control loops started");
    }

    public void IncreaseSetpoint()
    {
        var newSetpoint = _setpoint + _config.SetpointStep;
        if (newSetpoint <= _config.MaxSetpoint)
        {
            _setpoint = newSetpoint;
            _uart.SendSetpoint(_setpoint);
            Console.WriteLine($"[Thermostat] Setpoint increased to {_setpoint}°{(_useFahrenheit ? "F" : "C")}");
            UpdateDisplay();
        }
    }

    public void DecreaseSetpoint()
    {
        var newSetpoint = _setpoint - _config.SetpointStep;
        if (newSetpoint >= _config.MinSetpoint)
        {
            _setpoint = newSetpoint;
            _uart.SendSetpoint(_setpoint);
            Console.WriteLine($"[Thermostat] Setpoint decreased to {_setpoint}°{(_useFahrenheit ? "F" : "C")}");
            UpdateDisplay();
        }
    }

    public void CycleMode()
    {
        _currentMode = _currentMode switch
        {
            ThermostatMode.Off => ThermostatMode.Cool,
            ThermostatMode.Cool => ThermostatMode.Heat,
            ThermostatMode.Heat => ThermostatMode.Auto,
            ThermostatMode.Auto => ThermostatMode.Off,
            _ => ThermostatMode.Off
        };
        
        _uart.SendMode(_currentMode);
        Console.WriteLine($"[Thermostat] Mode changed to {_currentMode}");
        UpdateDisplay();
    }

    private async Task SensorReadLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Read temperature and humidity
                var (temp, humidity) = _sensor.ReadMeasurement(_useFahrenheit);
                _currentTemperature = temp;
                _currentHumidity = humidity;
                
                // Send temperature to relay box
                _uart.SendTemperature(_currentTemperature);
                
                // Update display
                UpdateDisplay();
                
                await Task.Delay(_config.ReadingIntervalSeconds * 1000, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Console.WriteLine($"[Thermostat] Sensor read error: {ex.Message}");
                await Task.Delay(5000, cancellationToken);
            }
        }
    }

    private async Task ControlLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Check for safety violations
                CheckSafetyInterlocks();
                
                // Request status update from relay box
                _uart.RequestStatus();
                
                await Task.Delay(5000, cancellationToken); // Control loop every 5 seconds
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Console.WriteLine($"[Thermostat] Control loop error: {ex.Message}");
                await Task.Delay(5000, cancellationToken);
            }
        }
    }

    private void CheckSafetyInterlocks()
    {
        // Check max runtime
        if (_systemRunStart.HasValue)
        {
            var runtime = (DateTime.UtcNow - _systemRunStart.Value).TotalSeconds;
            if (runtime > _config.MaxRunTimeSeconds)
            {
                Console.WriteLine($"[Thermostat] WARNING: Max runtime exceeded ({runtime}s)");
                // Relay box should handle this, but log it
            }
        }
        
        // Check compressor delay
        if (_lastCompressorStop.HasValue && _systemStatus == SystemStatus.WaitingCompressorDelay)
        {
            var delaySince = (DateTime.UtcNow - _lastCompressorStop.Value).TotalSeconds;
            if (delaySince < _config.CompressorDelaySeconds)
            {
                Console.WriteLine($"[Thermostat] Compressor delay: {_config.CompressorDelaySeconds - delaySince:F0}s remaining");
            }
        }
    }

    private void OnRelayBoxStatusChanged(object? sender, SystemStatus status)
    {
        var oldStatus = _systemStatus;
        _systemStatus = status;
        
        Console.WriteLine($"[Thermostat] Status changed: {oldStatus} -> {status}");
        
        // Track compressor stop time for delay enforcement
        if (oldStatus == SystemStatus.Cooling && status != SystemStatus.Cooling)
        {
            _lastCompressorStop = DateTime.UtcNow;
        }
        
        // Track system run start
        if (status == SystemStatus.Cooling || status == SystemStatus.Heating)
        {
            if (!_systemRunStart.HasValue)
            {
                _systemRunStart = DateTime.UtcNow;
            }
        }
        else
        {
            _systemRunStart = null;
        }
        
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        try
        {
            _display.DrawTemperatureScreen(
                currentTemp: _currentTemperature,
                setpoint: _setpoint,
                status: _systemStatus,
                mode: _currentMode,
                humidity: _currentHumidity
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Thermostat] Display update error: {ex.Message}");
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        _sensorReadTask?.Wait(TimeSpan.FromSeconds(5));
        _controlLoopTask?.Wait(TimeSpan.FromSeconds(5));
        Console.WriteLine("[Thermostat] Control loops stopped");
    }
}
