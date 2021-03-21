using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CallMonitor;
using CallMonitor.Configuration;
using Microsoft.Extensions.Options;

namespace CallMonitor
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly NetworkListener _listener;
        private readonly TrafficMonitor _monitor;
        private readonly IOptions<Application> _appConfig;

        public Worker(ILogger<Worker> logger, NetworkListener listener, TrafficMonitor monitor, IOptions<Application> appConfig) =>
            (_logger, _listener, _monitor, _appConfig) = (logger, listener, monitor, appConfig);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _monitor.OnUnknownProvider += OnUnknownProvider;
            _monitor.OnCallStarted += OnCallStarted;
            _monitor.OnCallEnded += OnCallEnded;
            _listener.OnUDPTafficeReceived += (IPHeader ipHeader) => _monitor.Received(ipHeader);

            _listener.Start();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(1000, stoppingToken);
                    _monitor.CheckStats();
                }
                catch (Exception e)
                {
                    _logger.LogError(e, String.Empty);
                } 
            }
        }

        private void OnUnknownProvider(string ip, int packetCount)
        {
            _logger.LogDebug($"Unmapped network {ip} count {packetCount}");
        }

        private void OnCallStarted(string ip, string provider, int packetCount)
        {
            _logger.LogInformation($"call started for {provider} on {ip} count {packetCount}");
            if (Uri.TryCreate(_appConfig.Value.CallStartWebhook, UriKind.Absolute, out Uri uri))
            {
                Task.Run( () => Webhook.Invoke(uri, new { provider }));
            }
        } 

        private void OnCallEnded(TimeSpan ts)
        {
            _logger.LogInformation($"call ended, time {(int)ts.TotalSeconds} seconds");
            if (Uri.TryCreate(_appConfig.Value.CallEndWebhook, UriKind.Absolute, out Uri uri))
            {
                Task.Run(() => Webhook.Invoke(uri, new { duration = ts.TotalSeconds }));
            }
        }
    }
}
