using System;
using System.Device.I2c;
using Iot.Device.Sht3x;

namespace TrailerAutomationClientNet
{
    /// <summary>
    /// Thin wrapper around the SHT3x (SHT31) temperature / humidity sensor
    /// on Raspberry Pi (I2C bus 1, address 0x44 by default).
    /// </summary>
    public sealed class Sht31Reader : IDisposable
    {
        private readonly I2cDevice _i2cDevice;
        private readonly Sht3x _sensor;
        private bool _disposed;

        /// <summary>
        /// Create a new SHT31 reader.
        /// On Raspberry Pi, I2C bus is typically 1 and the default address is 0x44.
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
        /// Reads only the temperature in Celsius.
        /// </summary>
        public double ReadTemperatureC()
        {
            EnsureNotDisposed();
            // Temperature is a UnitsNet.Temperature, use DegreesCelsius.
            return _sensor.Temperature.DegreesCelsius;
        }

        /// <summary>
        /// Reads only the relative humidity in percent (0–100).
        /// </summary>
        public double ReadHumidityPercent()
        {
            EnsureNotDisposed();
            // Humidity is UnitsNet.RelativeHumidity, use Percent.
            return _sensor.Humidity.Percent;
        }

        /// <summary>
        /// Reads both temperature (°C) and humidity (%) in a single call.
        /// </summary>
        public (double TemperatureC, double HumidityPercent) ReadMeasurement()
        {
            EnsureNotDisposed();
            var temperatureC = _sensor.Temperature.DegreesCelsius;
            var humidityPercent = _sensor.Humidity.Percent;
            return (temperatureC, humidityPercent);
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
}
