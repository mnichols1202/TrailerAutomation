using System.Device.I2c;
using Iot.Device.Sht3x;

namespace TrailerAutomationThermostatNet;

/// <summary>
/// Thin wrapper around the SHT3x (SHT31) temperature / humidity sensor
/// for the thermostat controller.
/// </summary>
public sealed class Sht31Reader : IDisposable
{
    private readonly I2cDevice _i2cDevice;
    private readonly Sht3x _sensor;
    private bool _disposed;

    /// <summary>
    /// Create a new SHT31 reader.
    /// </summary>
    /// <param name="busId">I2C bus ID. On Pi this is almost always 1.</param>
    /// <param name="deviceAddress">7-bit I2C address. Default SHT3x address is 0x44.</param>
    public Sht31Reader(int busId = 1, int deviceAddress = 0x44)
    {
        var connectionSettings = new I2cConnectionSettings(busId, deviceAddress);
        _i2cDevice = I2cDevice.Create(connectionSettings);
        _sensor = new Sht3x(_i2cDevice);
    }

    /// <summary>
    /// Reads temperature in Celsius.
    /// </summary>
    public double ReadTemperatureC()
    {
        EnsureNotDisposed();
        return _sensor.Temperature.DegreesCelsius;
    }

    /// <summary>
    /// Reads temperature in Fahrenheit.
    /// </summary>
    public double ReadTemperatureF()
    {
        EnsureNotDisposed();
        return _sensor.Temperature.DegreesFahrenheit;
    }

    /// <summary>
    /// Reads relative humidity in percent (0–100).
    /// </summary>
    public double ReadHumidityPercent()
    {
        EnsureNotDisposed();
        return _sensor.Humidity.Percent;
    }

    /// <summary>
    /// Reads both temperature and humidity in a single call.
    /// </summary>
    /// <param name="useFahrenheit">If true, temperature is in Fahrenheit; otherwise Celsius.</param>
    public (double Temperature, double HumidityPercent) ReadMeasurement(bool useFahrenheit = true)
    {
        EnsureNotDisposed();
        var temperature = useFahrenheit 
            ? _sensor.Temperature.DegreesFahrenheit 
            : _sensor.Temperature.DegreesCelsius;
        var humidity = _sensor.Humidity.Percent;
        return (temperature, humidity);
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(Sht31Reader));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _sensor.Dispose();
        _i2cDevice.Dispose();
        _disposed = true;
    }
}
