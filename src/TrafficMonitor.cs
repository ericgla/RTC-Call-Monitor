using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RtcCallMonitor.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;

namespace RtcCallMonitor
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
        private NetworkListener _listener;
        private IPAddress _ipAddress = IPAddress.Loopback;
        private readonly IOptions<Application> _appConfig;

        public delegate void UnknownNetwork(string ip, int packetCount);
        public delegate void CallStart(string ip, string provider, int packetCount);
        public delegate void CallEnd(TimeSpan ts);

        public event UnknownNetwork OnUnknownProvider;
        public event CallStart OnCallStarted;
        public event CallEnd OnCallEnded;

        public TrafficMonitor(ILogger<TrafficMonitor> logger, IOptions<Provider> config, NetworkListener listener, IOptions<Application> appConfig)
        {
            _logger = logger;
            _listener = listener;
            _appConfig = appConfig;

            // build known networks for each defined call provider
            foreach (var p in config.Value.KnownNetworks)
            {
                var cidrList = new List<IPNetwork>();
                foreach (var a in p.Value)
                {
                    cidrList.Add(IPNetwork.Parse(a));
                }
                _knownNetworks.Add(p.Key, cidrList.ToArray());
                _logger.LogInformation($"loaded provider {p.Key}, {p.Value.Count()} network prefixes");
            }
        }

        public void Start()
        {
            _listener.OnUDPTafficeReceived += (IPHeader ipHeader) =>
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
            };

            NetworkChange.NetworkAddressChanged += NetworkChange_NetworkAddressChanged;
            NetworkChange_NetworkAddressChanged(null, null);
        }

        private void NetworkChange_NetworkAddressChanged(object sender, EventArgs e)
        {
            _logger.LogInformation($"network changed!");
            Thread.Sleep(_appConfig.Value.DelayMs);
            IPAddress newIpAddress = IPAddress.Loopback;
            try
            {
                /* 
                 * Attempt to detect the current IP address by defining a fake connection to an arbitrary remote IP
                 * This implementation is cross-platform and should work on Windows/Linux
                 */
                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                {
                    socket.Connect("8.8.8.8", 65530);
                    if (socket.LocalEndPoint is IPEndPoint endPoint)
                    {
                        newIpAddress = endPoint.Address;
                        _logger.LogInformation($"Auto-detected IP address: {newIpAddress}");
                    }
                    else
                    {
                        _logger.LogWarning("Failed to auto-detect IP adress!");
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Unable to detect correct IP address - no valid network available. Exception type: {FullName}", ex.GetType().FullName);
                _logger.LogWarning($"Defaulting IP address to loopback: {newIpAddress}");
            }

            if ((newIpAddress != IPAddress.Loopback) && (_ipAddress != newIpAddress))
            {
                _ipAddress = newIpAddress;
                _listener.UpdateIpAddress(_ipAddress);
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
