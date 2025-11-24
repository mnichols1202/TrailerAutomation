using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Makaretu.Dns;

namespace TrailerAutomationClientNet
{
    public static class GatewayDiscovery
    {
        // Service type we advertise on the gateway side
        // e.g. new ServiceProfile("TrailerAutomationGateway", "_trailer-gateway._tcp", 5000);
        private const string ServiceType = "_trailer-gateway._tcp";

        /// <summary>
        /// Discover the TrailerAutomationGateway via mDNS/DNS-SD.
        /// Returns (IP, port) or null on timeout.
        /// </summary>
        public static async Task<(IPAddress ip, int port)?> DiscoverAsync(TimeSpan? timeout = null)
        {
            timeout ??= TimeSpan.FromSeconds(5);

            // mDNS transport + higher-level DNS-SD helper
            using var mdns = new MulticastService();
            using var sd = new ServiceDiscovery(mdns);

            var tcs = new TaskCompletionSource<(IPAddress ip, int port)?>(TaskCreationOptions.RunContinuationsAsynchronously);

            // We capture the last SRV record we see that matches our service
            DomainName? targetHost = null;
            ushort? targetPort = null;

            // Fired when a service instance PTR is discovered
            void OnServiceInstanceDiscovered(object? sender, ServiceInstanceDiscoveryEventArgs e)
            {
                // Example: "TrailerAutomationGateway._trailer-gateway._tcp.local"
                var instanceName = e.ServiceInstanceName.ToString();

                if (!instanceName.Contains(ServiceType, StringComparison.OrdinalIgnoreCase))
                    return;

                // Request SRV for this instance (host + port)
                mdns.SendQuery(e.ServiceInstanceName, DnsClass.IN, DnsType.SRV);
            }

            // Fired whenever any mDNS answer is received
            void OnAnswerReceived(object? sender, MessageEventArgs e)
            {
                // 1) Look for SRV records (service instance -> host + port)
                var srvRecords = e.Message.Answers
                    .OfType<SRVRecord>()
                    .Where(r => r.Name.ToString().Contains(ServiceType, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (srvRecords.Any())
                {
                    var srv = srvRecords.First();
                    targetHost = srv.Target;
                    targetPort = srv.Port;

                    // Ask for A/AAAA records for the host
                    mdns.SendQuery(srv.Target, DnsClass.IN, DnsType.A);
                    mdns.SendQuery(srv.Target, DnsClass.IN, DnsType.AAAA);
                    return;
                }

                // 2) If we already know the host+port, look for A/AAAA for that host
                if (targetHost is null || targetPort is null)
                    return;

                var addrRecords = e.Message.Answers
                    .OfType<AddressRecord>()
                    .Where(a => a.Name.Equals(targetHost))
                    .ToList();

                if (!addrRecords.Any())
                    return;

                // Prefer IPv4 (simpler for Pi + your current stack)
                var ipv4 = addrRecords
                    .Select(a => a.Address)
                    .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);

                if (ipv4 != null && !tcs.Task.IsCompleted)
                {
                    tcs.TrySetResult((ipv4, targetPort.Value));
                }
            }

            // Wire up events
            sd.ServiceInstanceDiscovered += OnServiceInstanceDiscovered;
            mdns.AnswerReceived += OnAnswerReceived;

            // Start mDNS and send query for our service instances
            mdns.Start();

            var serviceDomain = new DomainName(ServiceType); // "_trailer-gateway._tcp"
            sd.QueryServiceInstances(serviceDomain);

            // Wait for discovery or timeout
            using var cts = new CancellationTokenSource(timeout.Value);

            await using var _ = cts.Token.Register(() =>
            {
                if (!tcs.Task.IsCompleted)
                    tcs.TrySetResult(null);
            });

            var result = await tcs.Task;

            // Cleanup event handlers before returning
            sd.ServiceInstanceDiscovered -= OnServiceInstanceDiscovered;
            mdns.AnswerReceived -= OnAnswerReceived;

            return result;
        }
    }
}
