using System;
using System.Collections.Generic;

namespace TrailerAutomationGateway
{
    public class OtaFirmwareStore
    {
        private readonly record struct Entry(byte[] Data, string Filename, DateTime UploadedAtUtc);

        private readonly Dictionary<string, Entry> _store = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new();

        public void Store(string deviceId, byte[] data, string filename)
        {
            lock (_lock)
                _store[deviceId] = new Entry(data, filename, DateTime.UtcNow);
        }

        public (byte[] Data, string Filename)? Get(string deviceId)
        {
            lock (_lock)
                return _store.TryGetValue(deviceId, out var e) ? (e.Data, e.Filename) : null;
        }

        public void Remove(string deviceId)
        {
            lock (_lock)
                _store.Remove(deviceId);
        }
    }
}
