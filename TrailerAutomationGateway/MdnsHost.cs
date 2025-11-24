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
        private const string ServiceType = "_trailer-gateway._tcp";

        public static void Start(string instanceName, int port)
        {
            if (_mdns != null)
            {
                // Already started
                return;
            }

            // Multicast mDNS engine
            _mdns = new MulticastService();

            // DNS-SD wrapper
            _serviceDiscovery = new ServiceDiscovery(_mdns);

            // This ctor is valid and documented: ServiceProfile("instance", "service._tcp", port)
            // See example in docs: new ServiceProfile("x", "_myservice._udp", 1024);
            _profile = new ServiceProfile(instanceName, ServiceType, (ushort)port);

            // Advertise the service
            _serviceDiscovery.Advertise(_profile);

            // Start listening/responding on 224.0.0.251/5353 (mDNS)
            _mdns.Start();
        }

        public static void Stop()
        {
            if (_serviceDiscovery != null && _profile != null)
            {
                _serviceDiscovery.Unadvertise(_profile);
            }

            _serviceDiscovery?.Dispose();
            _serviceDiscovery = null;

            _mdns?.Stop();
            _mdns = null;
            _profile = null;
        }
    }
}
