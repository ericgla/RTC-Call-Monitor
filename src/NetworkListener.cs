using RtcCallMonitor.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace RtcCallMonitor
{
    public class NetworkListener
    {
        private Socket _socket;
        private IPAddress _localIp;
        private IPNetwork _localCIDR;
        private readonly byte[] _buffer = new byte[65507];  // max UDP packet size

        private readonly ILogger<NetworkListener> _logger;
        private readonly IOptions<Application> _appConfig;

        public delegate void Notify(IPHeader ipHeader);

        public event Notify OnUDPTafficeReceived;

        private Socket CreateAndBindSocket()
        {
            const int SIO_RCVALL = unchecked((int)0x98000001);

            var s = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Udp);

            s.Bind(new IPEndPoint(_localIp, 0));
            s.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.HeaderIncluded, true);

            byte[] IN = new byte[4] { 1, 0, 0, 0 };
            byte[] OUT = new byte[4];

            s.IOControl(SIO_RCVALL, IN, OUT);

            if (BitConverter.ToInt32(OUT) != 0) throw new Exception("Got non-zero result from IOControl");

            return s;
        }

        public NetworkListener(ILogger<NetworkListener> logger, IOptions<Application> appConfig) => 
            (_logger, _appConfig) = (logger, appConfig);

        public void Start()
        {
            _localCIDR = IPNetwork.Parse(_appConfig.Value.LocalNetwork);

            _localIp = Dns.GetHostEntry(Dns.GetHostName()).AddressList?
                .Where(ip => ip.AddressFamily == AddressFamily.InterNetwork && _localCIDR.Contains(ip))
                .FirstOrDefault();

            if (_localIp == null)
            {
                throw new Exception($"unable to find a local ip address within network {_appConfig.Value.LocalNetwork}");
            }

            _socket = CreateAndBindSocket();

            _logger.LogInformation($"monitoring traffic on {_localIp}");

            _socket.BeginReceive(_buffer, 0, _buffer.Length, SocketFlags.None, new AsyncCallback(OnReceive), null);
        }

        public void Stop() => _socket.Close();
         
        private void OnReceive(IAsyncResult result)
        {
            try
            {
                IPHeader ipHeader = new(_buffer, _socket.EndReceive(result), _localIp);
                if (IsOutsideUDPTaffice(ipHeader))
                {
                    OnUDPTafficeReceived?.Invoke(ipHeader);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "OnReceive Exception");
            }

            try
            {
                _socket.BeginReceive(_buffer, 0, _buffer.Length, SocketFlags.None, new AsyncCallback(OnReceive), null);
            }
            catch (ObjectDisposedException) { }
        }

        private bool IsOutsideUDPTaffice(IPHeader header)
        {
            if (header.IsUDP && !header.IsMulticast && !header.IsBroadcast)
            {
                if (header.SourceAddress.Equals(_localIp) || header.DestinationAddress.Equals(_localIp))
                {
                    return !(_localCIDR.Contains(header.SourceAddress) && _localCIDR.Contains(header.DestinationAddress));
                }
            }
            return false;
        }
    }
}
