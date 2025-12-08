namespace TrailerAutomationThermostatNet;

/// <summary>
/// Thermostat operating modes.
/// </summary>
public enum ThermostatMode
{
    Off,
    Cool,
    Heat,
    Auto
}

/// <summary>
/// Current system status.
/// </summary>
public enum SystemStatus
{
    Idle,
    Cooling,
    Heating,
    WaitingCompressorDelay,
    Error
}

/// <summary>
/// Button actions for user interface.
/// </summary>
public enum ButtonAction
{
    IncreaseSetpoint,
    DecreaseSetpoint,
    CycleMode,
    ToggleDisplay
}
