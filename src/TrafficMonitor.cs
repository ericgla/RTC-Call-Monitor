using CallMonitor.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace CallMonitor
{
    public class TrafficMonitor
    {
        private readonly Dictionary<string, IPNetwork[]> _knownNetworks = new();
        private readonly int _minPacketRate = 1;
        private bool _callActive;
        private string _callIP;
        private DateTimeOffset _callStart;
        private Dictionary<string, int> _inboundStats = new();
        private Dictionary<string, int> _outboundStats = new();
        private ILogger<TrafficMonitor> _logger;

        public delegate void UnknownNetwork(string ip, int packetCount);
        public delegate void CallStart(string ip, string provider, int packetCount);
        public delegate void CallEnd(TimeSpan ts);

        public event UnknownNetwork OnUnknownProvider;
        public event CallStart OnCallStarted;
        public event CallEnd OnCallEnded;

        public TrafficMonitor(ILogger<TrafficMonitor> logger, IOptions<Provider> config)
        {
            _logger = logger;

            // build known networks for each defined call provider
            foreach (var p in config.Value.KnownNetworks)
            {
                var cidrList = new List<IPNetwork>();
                foreach (var a in p.Value)
                {
                    cidrList.Add(IPNetwork.Parse(a));
                }
                _knownNetworks.Add(p.Key, cidrList.ToArray());
            }
        }

        public void Received(IPHeader ipHeader)
        {
            if (ipHeader.Inbound)
            {
                var source = ipHeader.SourceAddress.ToString();
                if (!_inboundStats.ContainsKey(source))
                {
                    _inboundStats.Add(source, 0);
                }
                _inboundStats[source]++;
            }
            else
            {
                var dest = ipHeader.DestinationAddress.ToString();
                if (!_outboundStats.ContainsKey(dest))
                {
                    _outboundStats.Add(dest, 0);
                }
                _outboundStats[dest]++;
            }
        }

        public void CheckStats()
        {
            // if there is an active call, filter by the active call ip
            // otherwise filter for the top traffic producer for the period
            var topInbound = _inboundStats
                .Where(x => !_callActive || x.Key == _callIP)
                .OrderByDescending(x => x.Value)
                .FirstOrDefault();

            var topOutbound = _outboundStats
                .Where(x => !_callActive || x.Key == _callIP)
                .OrderByDescending(x => x.Value)
                .FirstOrDefault();

            var (ip, rate) = topInbound.Value > topInbound.Value ? topInbound : topOutbound;

            if (rate >= _minPacketRate)
            {
                var provider = _knownNetworks
                .SelectMany(p => p.Value, (kvp, ip) => (kvp.Key, ip))
                .Where(x => x.ip.Contains(IPAddress.Parse(ip)))
                .Select(x => x.Key)
                .FirstOrDefault();

                if (provider == null)
                {
                    OnUnknownProvider.Invoke(ip, rate);
                }
                else if (!_callActive)
                {
                    _callStart = DateTimeOffset.Now;
                    _callActive = true;
                    _callIP = ip;
                    OnCallStarted.Invoke(ip, provider, rate);
                }
            }
            else
            {
                if (_callActive)
                {
                    _callActive = false;
                    _callIP = null;
                    OnCallEnded.Invoke(DateTimeOffset.Now - _callStart);
                }
            }
            _inboundStats.Clear();
            _outboundStats.Clear();
        }
    }
}
