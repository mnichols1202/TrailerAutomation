using System;
using System.Collections.Generic;
using System.Linq;

namespace TrailerAutomationGateway
{
    /// <summary>
    /// In-memory registry of devices with their command endpoint information.
    /// Maintains device IP addresses, command ports, and capabilities for TCP command routing.
    /// </summary>
    public class DeviceRegistry
    {
        private readonly object _sync = new();
        private readonly Dictionary<string, DeviceInfo> _devices = new();

        /// <summary>
        /// Register or update a device with its command endpoint information.
        /// </summary>
        public void RegisterDevice(string deviceId, string ipAddress, int commandPort, string[] capabilities)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                throw new ArgumentException("DeviceId is required.", nameof(deviceId));
            }

            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                throw new ArgumentException("IpAddress is required.", nameof(ipAddress));
            }

            if (commandPort <= 0 || commandPort > 65535)
            {
                throw new ArgumentException("CommandPort must be between 1 and 65535.", nameof(commandPort));
            }

            lock (_sync)
            {
                var now = DateTime.UtcNow;

                if (_devices.TryGetValue(deviceId, out var existing))
                {
                    // Update existing device
                    existing.IpAddress = ipAddress;
                    existing.CommandPort = commandPort;
                    existing.Capabilities = capabilities ?? Array.Empty<string>();
                    existing.LastSeen = now;

                    Console.WriteLine(
                        $"[DeviceRegistry][UPDATE] {deviceId} " +
                        $"IP={ipAddress} Port={commandPort} " +
                        $"Capabilities=[{string.Join(", ", capabilities ?? Array.Empty<string>())}] " +
                        $"LastSeen={now:O}");
                }
                else
                {
                    // New device
                    var device = new DeviceInfo
                    {
                        DeviceId = deviceId,
                        IpAddress = ipAddress,
                        CommandPort = commandPort,
                        Capabilities = capabilities ?? Array.Empty<string>(),
                        LastSeen = now
                    };

                    _devices[deviceId] = device;

                    Console.WriteLine(
                        $"[DeviceRegistry][NEW] {deviceId} " +
                        $"IP={ipAddress} Port={commandPort} " +
                        $"Capabilities=[{string.Join(", ", capabilities ?? Array.Empty<string>())}] " +
                        $"LastSeen={now:O}");
                }
            }
        }

        /// <summary>
        /// Get a single device by ID.
        /// </summary>
        public DeviceInfo? GetDevice(string deviceId)
        {
            lock (_sync)
            {
                if (_devices.TryGetValue(deviceId, out var device))
                {
                    return device.Clone();
                }

                return null;
            }
        }

        /// <summary>
        /// Get all registered devices.
        /// </summary>
        public IReadOnlyCollection<DeviceInfo> GetAllDevices()
        {
            lock (_sync)
            {
                return _devices.Values
                    .Select(d => d.Clone())
                    .ToList()
                    .AsReadOnly();
            }
        }

        /// <summary>
        /// Remove a device from the registry.
        /// </summary>
        public bool RemoveDevice(string deviceId)
        {
            lock (_sync)
            {
                if (_devices.Remove(deviceId))
                {
                    Console.WriteLine($"[DeviceRegistry][REMOVED] {deviceId}");
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Remove devices not seen since the specified cutoff time.
        /// </summary>
        public int RemoveStaleDevices(DateTime cutoffUtc)
        {
            lock (_sync)
            {
                var staleDevices = _devices
                    .Where(kvp => kvp.Value.LastSeen < cutoffUtc)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var deviceId in staleDevices)
                {
                    _devices.Remove(deviceId);
                    Console.WriteLine(
                        $"[DeviceRegistry][REMOVED] {deviceId} " +
                        $"Reason=Stale (not seen since before {cutoffUtc:O})");
                }

                return staleDevices.Count;
            }
        }
    }
}
