using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace TrailerAutomationGateway
{
    /// <summary>
    /// Stores the most recent sensor reading per client in memory.
    /// </summary>
    public sealed class SensorReadingRegistry
    {
        // Key: ClientId
        private readonly ConcurrentDictionary<string, SensorReading> _readings =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Registers or updates the latest sensor reading for a client.
        /// </summary>
        public void RegisterReading(
            string? clientId,
            double temperatureC,
            double humidityPercent,
            DateTime timestampUtc,
            string? remoteIp)
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                return;
            }

            var reading = new SensorReading
            {
                ClientId = clientId,
                TemperatureC = temperatureC,
                HumidityPercent = humidityPercent,
                TimestampUtc = timestampUtc,
                RemoteIp = remoteIp
            };

            // FIX: removed "static", normal lambda allowed to capture "reading"
            _readings.AddOrUpdate(clientId, reading, (_, _) => reading);
        }

        /// <summary>
        /// Returns all latest readings for all clients.
        /// </summary>
        public IReadOnlyCollection<SensorReading> GetAllReadings()
        {
            // ToList() gives a concrete IReadOnlyCollection
            return _readings.Values.ToList();
        }

        /// <summary>
        /// Returns the latest reading for the specified client, or null if none.
        /// </summary>
        public SensorReading? GetReading(string clientId)
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                return null;
            }

            return _readings.TryGetValue(clientId, out var reading) ? reading : null;
        }
    }
}
