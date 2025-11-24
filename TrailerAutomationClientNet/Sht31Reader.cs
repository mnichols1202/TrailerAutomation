using System;
using System.Device.I2c;
using Iot.Device.Sht3x;

namespace TrailerAutomationClientNet
{
    public sealed class Sht31Reader : IDisposable
    {
        private readonly I2cDevice _i2cDevice;
        private readonly Sht3x _sensor;
        private bool _disposed;

        /// <summary>
        /// Create a new SHT31 reader.
        /// On Raspberry Pi, I2C bus is typically 1 and the default address is 0x44.
        /// </summary>
        /// <param name="i2cBusId">I2C bus number (default 1 on Raspberry Pi).</param>
        /// <param name="i2cAddress">Sensor I2C address (0x44 or 0x45).</param>
        public Sht31Reader(int i2cBusId = 1, int i2cAddress = 0x44)
        {
            var connectionSettings = new I2cConnectionSettings(i2cBusId, i2cAddress);
            _i2cDevice = I2cDevice.Create(connectionSettings);

            // Create SHT3x driver
            _sensor = new Sht3x(_i2cDevice);
        }

        /// <summary>
        /// Read temperature in degrees Celsius.
        /// </summary>
        public double ReadTemperatureCelsius()
        {
            EnsureNotDisposed();
            // The Sht3x driver exposes Temperature as a property
            return _sensor.Temperature.DegreesCelsius;
        }

        /// <summary>
        /// Read relative humidity in percent.
        /// </summary>
        public double ReadHumidityPercent()
        {
            EnsureNotDisposed();
            // The Sht3x driver exposes Humidity as a property
            return _sensor.Humidity.Percent;
        }

        /// <summary>
        /// Read both temperature (°C) and humidity (%) in one call.
        /// </summary>
        public (double TemperatureCelsius, double HumidityPercent) ReadTemperatureAndHumidity()
        {
            EnsureNotDisposed();
            var temp = _sensor.Temperature.DegreesCelsius;
            var hum = _sensor.Humidity.Percent;
            return (temp, hum);
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

            _sensor?.Dispose();
            _i2cDevice?.Dispose();
            _disposed = true;
        }
    }
}
