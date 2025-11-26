using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Makaretu.Dns;

namespace TrailerAutomationGateway
{
    /// <summary>
    /// Simple mDNS/DNS-SD advertiser using Makaretu.Dns.Multicast.
    /// Advertises the TrailerAutomationGateway HTTP API on a given port.
    /// </summary>
    public static class MdnsHost
    {
        private static MulticastService? _mdns;
        private static ServiceDiscovery? _serviceDiscovery;
        private static ServiceProfile? _profile;

        // Service type for the gateway. You can change this string if you like.
        // Clients must use the exact same service type in their discovery code.
        private const string ServiceType = "_trailer-gateway._tcp";

        private static readonly object _syncRoot = new();

        public static void Start(string instanceName, int port)
        {
            lock (_syncRoot)
            {
                if (_mdns != null)
                    return;

                _mdns = new MulticastService();
                _mdns.QueryReceived += (_, e) =>
                {
                    try
                    {
                        var wantsOurService = e.Message.Questions.Any(q => q.Name.ToString().Contains(ServiceType, StringComparison.OrdinalIgnoreCase));
                        if (wantsOurService)
                        {
                            Console.WriteLine("[mDNS] Query received for service type: " + string.Join(", ", e.Message.Questions.Select(q => q.Name)));
                        }
                    }
                    catch { }
                };
                _mdns.NetworkInterfaceDiscovered += (_, __) =>
                {
                    try
                    {
                        if (_serviceDiscovery != null && _profile != null)
                            _serviceDiscovery.Advertise(_profile);
                    }
                    catch { }
                };

                _mdns.Start();

                _serviceDiscovery = new ServiceDiscovery(_mdns)
                {
                    AnswersContainsAdditionalRecords = true
                };

                // Filter and/or override to only real LAN IPv4 addresses (Ethernet/Wi‑Fi).
                var addresses = GetAdvertisableIPv4Addresses();
                Console.WriteLine("[mDNS] Advertising on addresses: " + string.Join(", ", addresses.Select(a => a.ToString())));

                var instanceDomain = new DomainName(instanceName);
                var serviceDomain = new DomainName(ServiceType);
                _profile = new ServiceProfile(instanceDomain, serviceDomain, (ushort)port, addresses);

                // Optional TXT for debugging.
                _profile.AddProperty("port", port.ToString());
                _profile.AddProperty("platform", Environment.OSVersion.Platform.ToString());

                _serviceDiscovery.Advertise(_profile);
                Console.WriteLine("[mDNS] Service advertised: " + instanceName + "." + ServiceType + ".local:" + port);
            }
        }

        public static void Stop()
        {
            lock (_syncRoot)
            {
                try
                {
                    if (_serviceDiscovery != null && _profile != null)
                    {
                        Console.WriteLine("[mDNS] Unadvertising profile.");
                        _serviceDiscovery.Unadvertise(_profile);
                    }
                }
                catch { }
                finally
                {
                    _profile = null;
                    _serviceDiscovery?.Dispose();
                    _serviceDiscovery = null;
                    if (_mdns != null)
                    {
                        try { _mdns.Stop(); } catch { }
                        _mdns.Dispose();
                        _mdns = null;
                    }
                }
            }
        }

        private static IPAddress[] GetAdvertisableIPv4Addresses()
        {
            // Optional override via env var: TA_ADVERTISE_IP=192.168.2.100
            var overrideIp = Environment.GetEnvironmentVariable("TA_ADVERTISE_IP");
            if (!string.IsNullOrWhiteSpace(overrideIp) && IPAddress.TryParse(overrideIp, out var ip) && ip.AddressFamily == AddressFamily.InterNetwork)
            {
                Console.WriteLine($"[mDNS] TA_ADVERTISE_IP is set. Forcing advertisement to {ip}");
                return new[] { ip };
            }

            try
            {
                var addrs = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
                    .Where(nic => nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                    .Where(nic => !IsVirtualNic(nic))
                    .SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
                    .Where(ua => ua.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Select(ua => ua.Address)
                    .Distinct()
                    .ToList();

                // Prefer RFC1918 residential ranges by ordering: 192.168/16, 10/8, 172.16/12, then others.
                addrs = addrs
                    .OrderByDescending(a => a.GetAddressBytes()[0] == 192 && a.GetAddressBytes()[1] == 168)
                    .ThenByDescending(a => a.GetAddressBytes()[0] == 10)
                    .ThenByDescending(a => a.GetAddressBytes()[0] == 172 && a.GetAddressBytes()[1] >= 16 && a.GetAddressBytes()[1] <= 31)
                    .ToList();

                return addrs.ToArray();
            }
            catch
            {
                // Fallback to Makaretu default if NIC inspection fails.
                return MulticastService.GetIPAddresses()
                    .Where(ip => ip.AddressFamily == AddressFamily.InterNetwork)
                    .ToArray();
            }
        }

        private static bool IsVirtualNic(NetworkInterface nic)
        {
            var name = (nic.Name + " " + nic.Description).ToLowerInvariant();
            // Common virtual adapter identifiers
            string[] markers = new[] { "hyper-v", "vethernet", "virtual", "docker", "wsl", "loopback", "tunnel", "vmware", "virtualbox", "default switch" };
            return markers.Any(m => name.Contains(m));
        }
    }
}
