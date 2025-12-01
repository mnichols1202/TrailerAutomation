using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace TrailerAutomationGateway
{
    /// <summary>
    /// Stores the most recent sensor reading per client in memory
    /// and persists all readings to the database.
    /// </summary>
    public sealed class SensorReadingRegistry
    {
        // In-memory cache: Key = ClientId
        private readonly ConcurrentDictionary<string, SensorReading> _readings =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly SensorReadingRepository _repository;

        public SensorReadingRegistry(SensorReadingRepository repository)
        {
            _repository = repository;

            // Load latest readings from database into memory on startup
            foreach (var reading in _repository.GetAllLatest())
            {
                _readings[reading.ClientId] = reading;
            }
        }

        /// <summary>
        /// Registers or updates the latest sensor reading for a client.
        /// Persists to database and updates in-memory cache.
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

            // Persist to database
            _repository.Insert(reading);

            // Update in-memory cache
            _readings.AddOrUpdate(clientId, reading, (_, _) => reading);
        }

        /// <summary>
        /// Returns all latest readings for all clients from memory cache.
        /// </summary>
        public IReadOnlyCollection<SensorReading> GetAllReadings()
        {
            return _readings.Values.ToList();
        }

        /// <summary>
        /// Returns the latest reading for the specified client from memory cache.
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
