using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using Makaretu.Dns;

namespace TrailerAutomationClientNet
{
    public static class GatewayDiscovery
    {
        private const string ServiceType = "_trailer-gateway._tcp";
        private const string ServiceDomain = "local";
        private static string FullServiceName => $"{ServiceType}.{ServiceDomain}"; // _trailer-gateway._tcp.local

        /// <summary>
        /// Discover the TrailerAutomationGateway via mDNS and return its HTTP Uri.
        /// </summary>
        public static async Task<Uri?> DiscoverAsync(TimeSpan? timeout = null)
        {
            timeout ??= TimeSpan.FromSeconds(8); // Slightly longer default.

            // Prefer IPv4 for mDNS on constrained networks
            MulticastService.UseIpv4 = true;
            MulticastService.UseIpv6 = false;

            using var mdns = new MulticastService();
            var tcs = new TaskCompletionSource<Uri?>(TaskCreationOptions.RunContinuationsAsynchronously);

            // Track SRV targets (hostnames) and their ports until we get A answers.
            var pendingSrvTargets = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);

            // Determine local subnets for IPv4 address preference.
            var localNets = GetLocalIPv4Networks();
            Console.WriteLine($"[mDNS] Local IPv4 interfaces: {string.Join(", ", localNets.Select(n => n.ip + "/" + n.mask))}");

            void ResolveWith(IPAddress ip, int port, string reason)
            {
                if (ip.AddressFamily != AddressFamily.InterNetwork)
                    return;
                Console.WriteLine($"[mDNS] Selecting {ip}:{port} ({reason})");
                var uri = new Uri($"http://{ip}:{port}/");
                if (!tcs.Task.IsCompleted)
                    tcs.TrySetResult(uri);
            }

            void TryResolve(MessageEventArgs e)
            {
                try
                {
                    // 1. Collect SRV records.
                    foreach (var srv in e.Message.Answers.Concat(e.Message.AdditionalRecords).OfType<SRVRecord>())
                    {
                        var srvName = srv.Name?.ToString() ?? string.Empty;
                        if (!srvName.Contains(ServiceType, StringComparison.OrdinalIgnoreCase))
                            continue;

                        var target = srv.Target?.ToString();
                        if (string.IsNullOrWhiteSpace(target))
                            continue;

                        if (!pendingSrvTargets.ContainsKey(target))
                            pendingSrvTargets[target] = srv.Port;

                        // First: ask OS resolver (Avahi/Bonjour) for the target; this usually returns the right LAN A record.
                        try
                        {
                            var osAddrs = Dns.GetHostAddresses(target)
                                              .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                                              .ToList();
                            if (osAddrs.Count > 0)
                            {
                                Console.WriteLine($"[mDNS] OS resolver {target} -> {string.Join(", ", osAddrs)}");
                                var osBest = PickBestIPv4(osAddrs, localNets);
                                if (osBest != null)
                                {
                                    ResolveWith(osBest, srv.Port, "OS resolver result");
                                    return;
                                }
                            }
                        }
                        catch { }

                        // Check for A records for this SRV target in this packet.
                        var candidateIPs = e.Message.Answers
                            .Concat(e.Message.AdditionalRecords)
                            .OfType<AddressRecord>()
                            .Where(a => string.Equals(a.Name?.ToString(), target, StringComparison.OrdinalIgnoreCase))
                            .Select(a => a.Address)
                            .Where(a => a != null && a.AddressFamily == AddressFamily.InterNetwork)
                            .Cast<IPAddress>()
                            .ToList();

                        if (candidateIPs.Count > 0)
                        {
                            Console.WriteLine($"[mDNS] SRV {target} port {srv.Port}, A candidates: {string.Join(", ", candidateIPs)}");

                            // Prefer only same-subnet candidates. If none, try responder IP on local subnet.
                            var sameSubnet = candidateIPs.Where(ip => IsOnAnyLocalSubnet(ip, localNets)).ToList();
                            if (sameSubnet.Count > 0)
                            {
                                var bestLocal = PickBestIPv4(sameSubnet, localNets);
                                if (bestLocal != null)
                                {
                                    ResolveWith(bestLocal, srv.Port, "A candidate on same subnet");
                                    return;
                                }
                            }

                            var responderIp = e.RemoteEndPoint?.Address;
                            if (responderIp != null && responderIp.AddressFamily == AddressFamily.InterNetwork && IsOnAnyLocalSubnet(responderIp, localNets))
                            {
                                ResolveWith(responderIp, srv.Port, "responder address on local subnet (candidates not local)");
                                return;
                            }

                            // Otherwise ignore non-local candidates and ask again for A.
                            Console.WriteLine($"[mDNS] Ignoring non-local A candidates; querying A for {target} again");
                            var aQuery2 = new Message();
                            aQuery2.Questions.Add(new Question { Name = target, Type = DnsType.A, Class = DnsClass.IN });
                            mdns.SendQuery(aQuery2);
                        }
                        else
                        {
                            // No A records present. Prefer the responder's source IP as a strong hint if it sits on our subnet.
                            var responderIp = e.RemoteEndPoint?.Address;
                            if (responderIp != null && responderIp.AddressFamily == AddressFamily.InterNetwork && IsOnAnyLocalSubnet(responderIp, localNets))
                            {
                                Console.WriteLine($"[mDNS] No A for {target}; using responder {responderIp} on local subnet");
                                ResolveWith(responderIp, srv.Port, "responder address on local subnet");
                                return;
                            }

                            // Ask for A records if still unresolved.
                            Console.WriteLine($"[mDNS] No A for {target}; querying for A");
                            var aQuery = new Message();
                            aQuery.Questions.Add(new Question { Name = target, Type = DnsType.A, Class = DnsClass.IN });
                            mdns.SendQuery(aQuery);
                        }
                    }

                    if (pendingSrvTargets.Count == 0)
                        return; // Nothing to match yet.

                    // 2. Resolve A records that may arrive separately
                    var addrs = e.Message.Answers
                        .Concat(e.Message.AdditionalRecords)
                        .OfType<AddressRecord>()
                        .Where(a => a.Address?.AddressFamily == AddressFamily.InterNetwork)
                        .ToList();

                    if (addrs.Count > 0)
                    {
                        Console.WriteLine($"[mDNS] Address records seen: {string.Join(", ", addrs.Select(a => a.Name + "=" + a.Address))}");
                    }

                    foreach (var group in addrs.GroupBy(a => a.Name?.ToString() ?? string.Empty))
                    {
                        if (string.IsNullOrWhiteSpace(group.Key))
                            continue;
                        if (!pendingSrvTargets.TryGetValue(group.Key, out var port))
                            continue;

                        var ips = group.Select(g => g.Address!).ToList();
                        var sameSubnet = ips.Where(ip => IsOnAnyLocalSubnet(ip, localNets)).ToList();
                        if (sameSubnet.Count > 0)
                        {
                            var best = PickBestIPv4(sameSubnet, localNets);
                            if (best != null)
                            {
                                ResolveWith(best, port, "A from later response on same subnet");
                                return;
                            }
                        }
                    }
                }
                catch { }
            }

            void OnAnswerReceived(object? sender, MessageEventArgs e)
            {
                if (tcs.Task.IsCompleted)
                    return;

                TryResolve(e);

                // Browse PTR responses and follow up with SRV queries for instances.
                foreach (var ptr in e.Message.Answers.Concat(e.Message.AdditionalRecords).OfType<PTRRecord>())
                {
                    if (!string.Equals(ptr.Name?.ToString(), FullServiceName, StringComparison.OrdinalIgnoreCase))
                        continue;
                    var instanceName = ptr.DomainName?.ToString();
                    if (string.IsNullOrWhiteSpace(instanceName))
                        continue;
                    Console.WriteLine($"[mDNS] PTR -> instance: {instanceName}");
                    var srvQuery = new Message();
                    srvQuery.Questions.Add(new Question { Name = instanceName, Type = DnsType.SRV, Class = DnsClass.IN });
                    mdns.SendQuery(srvQuery);
                }
            }

            mdns.AnswerReceived += OnAnswerReceived;
            mdns.Start();

            Console.WriteLine($"[mDNS] Browsing {FullServiceName} ...");
            var browse = new Message();
            browse.Questions.Add(new Question { Name = FullServiceName, Type = DnsType.PTR, Class = DnsClass.IN });
            mdns.SendQuery(browse);

            var defaultInstanceFqdn = $"TrailerAutomationGateway.{FullServiceName}"; // Instance.ServiceType.local
            var directSrv = new Message();
            directSrv.Questions.Add(new Question { Name = defaultInstanceFqdn, Type = DnsType.SRV, Class = DnsClass.IN });
            mdns.SendQuery(directSrv);

            var discoveryTask = tcs.Task;
            var timeoutTask = Task.Delay(timeout.Value);
            var completed = await Task.WhenAny(discoveryTask, timeoutTask).ConfigureAwait(false);

            mdns.AnswerReceived -= OnAnswerReceived;
            mdns.Stop();

            return completed == discoveryTask ? discoveryTask.Result : null;
        }

        private static IPAddress? PickBestIPv4(IEnumerable<IPAddress> ips, List<(IPAddress ip, IPAddress mask)> localNets)
        {
            var list = ips.Where(i => i.AddressFamily == AddressFamily.InterNetwork).ToList();
            if (list.Count == 0) return null;

            // 1) Prefer same-subnet as any local interface
            foreach (var ip in list)
            {
                if (IsOnAnyLocalSubnet(ip, localNets))
                    return ip;
            }

            // 2) Prefer 192.168.0.0/16
            var preferred192 = list.FirstOrDefault(i => { var x=i.GetAddressBytes(); return x[0]==192 && x[1]==168; });
            if (preferred192 != null) return preferred192;

            // 3) Prefer 10.0.0.0/8
            var preferred10 = list.FirstOrDefault(i => i.GetAddressBytes()[0] == 10);
            if (preferred10 != null) return preferred10;

            // 4) Prefer 172.16.0.0/12
            var a = list.FirstOrDefault(i => i.GetAddressBytes()[0] == 172 && i.GetAddressBytes()[1] >= 16 && i.GetAddressBytes()[1] <= 31);
            if (a != null) return a;

            // 5) Fallback first
            return list[0];
        }

        private static bool IsOnAnyLocalSubnet(IPAddress ip, List<(IPAddress ip, IPAddress mask)> localNets)
        {
            foreach (var (lip, mask) in localNets)
            {
                if (IsInSameSubnet(ip, lip, mask)) return true;
            }
            return false;
        }

        private static List<(IPAddress ip, IPAddress mask)> GetLocalIPv4Networks()
        {
            var results = new List<(IPAddress, IPAddress)>();
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up) continue;
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                    foreach (var uni in nic.GetIPProperties().UnicastAddresses)
                    {
                        if (uni.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        var mask = uni.IPv4Mask ?? IPAddress.Parse("255.255.255.0");
                        results.Add((uni.Address, mask));
                    }
                }
            }
            catch { }
            return results;
        }

        private static bool IsInSameSubnet(IPAddress a, IPAddress b, IPAddress mask)
        {
            var ab = a.GetAddressBytes();
            var bb = b.GetAddressBytes();
            var mb = mask.GetAddressBytes();
            if (ab.Length != bb.Length || ab.Length != mb.Length) return false;
            for (int i = 0; i < ab.Length; i++)
            {
                if ((ab[i] & mb[i]) != (bb[i] & mb[i])) return false;
            }
            return true;
        }
    }
}
