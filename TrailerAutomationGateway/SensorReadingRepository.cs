using System;
using System.Collections.Generic;
using System.Linq;
using LiteDB;

namespace TrailerAutomationGateway
{
    /// <summary>
    /// Handles persistent storage of all sensor readings using LiteDB.
    /// </summary>
    public sealed class SensorReadingRepository : IDisposable
    {
        private readonly LiteDatabase _db;
        private readonly ILiteCollection<SensorReading> _readings;

        public SensorReadingRepository(string databasePath = "data/sensor-readings.db")
        {
            // Ensure directory exists
            var dir = System.IO.Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }

            _db = new LiteDatabase(databasePath);
            _readings = _db.GetCollection<SensorReading>("sensor_readings");

            // Ensure indexes are created
            _readings.EnsureIndex(x => x.ClientId);
            _readings.EnsureIndex(x => x.TimestampUtc);
        }

        /// <summary>
        /// Insert a new sensor reading into the database.
        /// </summary>
        public void Insert(SensorReading reading)
        {
            _readings.Insert(reading);
        }

        /// <summary>
        /// Get the latest reading for a specific client.
        /// </summary>
        public SensorReading? GetLatest(string clientId)
        {
            return _readings
                .Find(x => x.ClientId == clientId)
                .OrderByDescending(x => x.TimestampUtc)
                .FirstOrDefault();
        }

        /// <summary>
        /// Get all latest readings (one per client).
        /// </summary>
        public IEnumerable<SensorReading> GetAllLatest()
        {
            return _readings
                .FindAll()
                .GroupBy(x => x.ClientId)
                .Select(g => g.OrderByDescending(x => x.TimestampUtc).First())
                .ToList();
        }

        /// <summary>
        /// Get historical readings for a client within a time range.
        /// </summary>
        public IEnumerable<SensorReading> GetHistory(
            string clientId,
            DateTime? fromUtc = null,
            DateTime? toUtc = null,
            int limit = 1000)
        {
            var query = _readings.Query()
                .Where(x => x.ClientId == clientId);

            if (fromUtc.HasValue)
            {
                query = query.Where(x => x.TimestampUtc >= fromUtc.Value);
            }

            if (toUtc.HasValue)
            {
                query = query.Where(x => x.TimestampUtc <= toUtc.Value);
            }

            return query
                .OrderByDescending(x => x.TimestampUtc)
                .Limit(limit)
                .ToList();
        }

        /// <summary>
        /// Get all readings within a time range (across all clients).
        /// </summary>
        public IEnumerable<SensorReading> GetAllHistory(
            DateTime? fromUtc = null,
            DateTime? toUtc = null,
            int limit = 1000)
        {
            var query = _readings.Query();

            if (fromUtc.HasValue)
            {
                query = query.Where(x => x.TimestampUtc >= fromUtc.Value);
            }

            if (toUtc.HasValue)
            {
                query = query.Where(x => x.TimestampUtc <= toUtc.Value);
            }

            return query
                .OrderByDescending(x => x.TimestampUtc)
                .Limit(limit)
                .ToList();
        }

        /// <summary>
        /// Delete readings older than a specified date (for data retention).
        /// </summary>
        public int DeleteOlderThan(DateTime cutoffUtc)
        {
            return _readings.DeleteMany(x => x.TimestampUtc < cutoffUtc);
        }

        /// <summary>
        /// Get count of total readings in database.
        /// </summary>
        public int GetTotalCount()
        {
            return _readings.Count();
        }

        /// <summary>
        /// Get count of readings for a specific client.
        /// </summary>
        public int GetCount(string clientId)
        {
            return _readings.Count(x => x.ClientId == clientId);
        }

        /// <summary>
        /// Delete all sensor readings from the database.
        /// </summary>
        public int DeleteAll()
        {
            return _readings.DeleteAll();
        }

        public void Dispose()
        {
            _db?.Dispose();
        }
    }
}
