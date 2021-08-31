using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using RtcCallMonitor.Configuration;
using Microsoft.Extensions.Options;

namespace RtcCallMonitor
{
    public class Worker : BackgroundService
    {
        private readonly IHostApplicationLifetime _host;
        private readonly ILogger<Worker> _logger;
        private readonly TrafficMonitor _monitor;
        private readonly IOptions<Application> _appConfig;

        public Worker(IHostApplicationLifetime host, ILogger<Worker> logger, NetworkListener listener, TrafficMonitor monitor, IOptions<Application> appConfig) =>
            (_host, _logger, _monitor, _appConfig) = (host, logger, monitor, appConfig);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _monitor.OnUnknownProvider += OnUnknownProvider;
            _monitor.OnCallStarted += OnCallStarted;
            _monitor.OnCallEnded += OnCallEnded;

            try
            {
                _monitor.Start();
            }
            catch (Exception e)
            {
                _logger.LogError(e.ToString());
                _host.StopApplication();
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_appConfig.Value.CheckInterval ?? 1000, stoppingToken);
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
