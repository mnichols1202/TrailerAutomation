using System;
using System.Linq;
using System.Net;
using Makaretu.Dns;

namespace TrailerAutomationGateway
{
    public static class MdnsHost
    {
        private static MulticastService? _mdns;
        private static ServiceDiscovery? _serviceDiscovery;
        private static ServiceProfile? _profile;
        private const string ServiceType = "_trailer-gateway._tcp"; // DNS-SD service type (without .local)
        private static readonly object _syncRoot = new();

        public static void Start(string instanceName, int port)
        {
            lock (_syncRoot)
            {
                if (_mdns != null)
                    return;

                _mdns = new MulticastService();

                // Diagnostics: log incoming queries for our service type.
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

                _mdns.NetworkInterfaceDiscovered += (_, args) =>
                {
                    try
                    {
                        Console.WriteLine("[mDNS] Interface discovered; re-advertising profile.");
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

                // Collect IPv4 addresses.
                var addresses = MulticastService
                    .GetIPAddresses()
                    .Where(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    .ToArray();

                Console.WriteLine("[mDNS] Advertising on addresses: " + string.Join(", ", addresses.Select(a => a.ToString())));

                // Use DomainName objects (Makaretu expects this form for proper record construction).
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
    }
}
