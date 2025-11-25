using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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

            using var mdns = new MulticastService();
            var tcs = new TaskCompletionSource<Uri?>(TaskCreationOptions.RunContinuationsAsynchronously);

            // Track SRV targets (hostnames) and their ports until we get A/AAAA answers.
            var pendingSrvTargets = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);

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
                        // Immediately ask for A/AAAA if we have not yet received them in same packet.
                        var needAddress = !e.Message.Answers.Concat(e.Message.AdditionalRecords).OfType<AddressRecord>().Any(a => string.Equals(a.Name?.ToString(), target, StringComparison.OrdinalIgnoreCase));
                        if (needAddress)
                        {
                            var aQuery = new Message();
                            aQuery.Questions.Add(new Question { Name = target, Type = DnsType.A, Class = DnsClass.IN });
                            mdns.SendQuery(aQuery);
                            var aaaaQuery = new Message();
                            aaaaQuery.Questions.Add(new Question { Name = target, Type = DnsType.AAAA, Class = DnsClass.IN });
                            mdns.SendQuery(aaaaQuery);
                        }
                    }

                    if (pendingSrvTargets.Count == 0)
                        return; // Nothing to match yet.

                    // 2. Match any A/AAAA records to pending targets.
                    foreach (var addr in e.Message.Answers.Concat(e.Message.AdditionalRecords).OfType<AddressRecord>())
                    {
                        var host = addr.Name?.ToString();
                        if (string.IsNullOrWhiteSpace(host))
                            continue;
                        if (pendingSrvTargets.TryGetValue(host, out var port))
                        {
                            var ip = addr.Address;
                            if (ip == null)
                                continue;
                            var uri = new Uri($"http://{ip}:{port}/");
                            if (!tcs.Task.IsCompleted)
                            {
                                tcs.TrySetResult(uri);
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

                // Process SRV/A/AAAA in this packet.
                TryResolve(e);

                // Browse PTR responses and follow up with SRV queries for instances.
                foreach (var ptr in e.Message.Answers.Concat(e.Message.AdditionalRecords).OfType<PTRRecord>())
                {
                    if (!string.Equals(ptr.Name?.ToString(), FullServiceName, StringComparison.OrdinalIgnoreCase))
                        continue;
                    // FIX: Makaretu.Dns PTRRecord exposes the pointed name as DomainName (not PointerDomainName)
                    var instanceName = ptr.DomainName?.ToString();
                    if (string.IsNullOrWhiteSpace(instanceName))
                        continue;
                    var srvQuery = new Message();
                    srvQuery.Questions.Add(new Question { Name = instanceName, Type = DnsType.SRV, Class = DnsClass.IN });
                    mdns.SendQuery(srvQuery);
                }
            }

            mdns.AnswerReceived += OnAnswerReceived;
            mdns.Start();

            // Initial PTR browse.
            var browse = new Message();
            browse.Questions.Add(new Question { Name = FullServiceName, Type = DnsType.PTR, Class = DnsClass.IN });
            mdns.SendQuery(browse);

            // Fallback: direct SRV query for generic instance name if user kept default instance.
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
    }
}
