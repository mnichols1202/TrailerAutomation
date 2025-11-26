using System;
using System.Collections.Generic;
using System.Linq;

namespace TrailerAutomationGateway
{
    /// <summary>
    /// In-memory registry of active clients based on their heartbeat.
    /// A client is removed after it misses a configurable number of heartbeat intervals.
    /// </summary>
    public class ClientRegistry
    {
        private readonly object _sync = new();
        private readonly Dictionary<string, ClientInfo> _clients = new();

        // Heartbeat config
        private readonly TimeSpan _heartbeatInterval;
        private readonly int _maxMissedHeartbeats;

        public ClientRegistry()
            : this(TimeSpan.FromSeconds(10), 3)
        {
        }

        public ClientRegistry(TimeSpan heartbeatInterval, int maxMissedHeartbeats)
        {
            _heartbeatInterval = heartbeatInterval;
            _maxMissedHeartbeats = maxMissedHeartbeats;
        }

        /// <summary>
        /// Update or create a client entry based on a heartbeat from that client.
        /// Also logs activity to the console.
        /// </summary>
        public void RegisterHeartbeat(string clientId, string? deviceType, string? friendlyName, string? remoteIp)
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new ArgumentException("ClientId is required.", nameof(clientId));
            }

            lock (_sync)
            {
                var now = DateTime.UtcNow;

                if (_clients.TryGetValue(clientId, out var info))
                {
                    // Existing client – update timestamps and optional info.
                    info.LastHeartbeatUtc = now;

                    if (!string.IsNullOrWhiteSpace(deviceType))
                    {
                        info.DeviceType = deviceType;
                    }

                    if (!string.IsNullOrWhiteSpace(friendlyName))
                    {
                        info.FriendlyName = friendlyName;
                    }

                    if (!string.IsNullOrWhiteSpace(remoteIp))
                    {
                        info.RemoteIp = remoteIp;
                    }

                    Console.WriteLine(
                        $"[Heartbeat][UPDATE] {clientId} " +
                        $"IP={info.RemoteIp ?? "n/a"} " +
                        $"Type={info.DeviceType ?? "n/a"} " +
                        $"Name={info.FriendlyName ?? "n/a"} " +
                        $"Last={info.LastHeartbeatUtc:O}");
                }
                else
                {
                    // New client
                    info = new ClientInfo
                    {
                        ClientId = clientId,
                        DeviceType = deviceType,
                        FriendlyName = friendlyName,
                        RemoteIp = remoteIp,
                        FirstSeenUtc = now,
                        LastHeartbeatUtc = now
                    };

                    _clients[clientId] = info;

                    Console.WriteLine(
                        $"[Heartbeat][NEW] {clientId} " +
                        $"IP={info.RemoteIp ?? "n/a"} " +
                        $"Type={info.DeviceType ?? "n/a"} " +
                        $"Name={info.FriendlyName ?? "n/a"} " +
                        $"FirstSeen={info.FirstSeenUtc:O}");
                }

                RemoveStaleClients_NoLock(now);
            }
        }

        private void RemoveStaleClients_NoLock(DateTime nowUtc)
        {
            var maxAge = TimeSpan.FromTicks(_heartbeatInterval.Ticks * _maxMissedHeartbeats);

            var stale = _clients
                .Where(kvp => nowUtc - kvp.Value.LastHeartbeatUtc > maxAge)
                .Select(kvp => kvp.Value)
                .ToList();

            foreach (var client in stale)
            {
                _clients.Remove(client.ClientId);
                Console.WriteLine(
                    $"[Heartbeat][REMOVED] {client.ClientId} " +
                    $"IP={client.RemoteIp ?? "n/a"} " +
                    $"Type={client.DeviceType ?? "n/a"} " +
                    $"Name={client.FriendlyName ?? "n/a"} " +
                    $"LastHeartbeat={client.LastHeartbeatUtc:O} " +
                    $"Reason=Missed more than {_maxMissedHeartbeats} heartbeats " +
                    $"(interval={_heartbeatInterval.TotalSeconds:F0}s)");
            }
        }

        /// <summary>
        /// Get a snapshot of all active clients (stale ones removed first).
        /// </summary>
        public IReadOnlyCollection<ClientInfo> GetAllClients()
        {
            lock (_sync)
            {
                RemoveStaleClients_NoLock(DateTime.UtcNow);

                return _clients.Values
                    .Select(c => c.Clone())
                    .ToList()
                    .AsReadOnly();
            }
        }

        /// <summary>
        /// Get a single client by id, or null if not found.
        /// </summary>
        public ClientInfo? GetClient(string clientId)
        {
            lock (_sync)
            {
                RemoveStaleClients_NoLock(DateTime.UtcNow);

                if (_clients.TryGetValue(clientId, out var info))
                {
                    return info.Clone();
                }

                return null;
            }
        }
    }
}
