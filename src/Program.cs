using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using CallMonitor;
using Microsoft.Extensions.Logging;
using CallMonitor.Configuration;

namespace CallMonitor
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", false, true)
            .Build();

            return Host.CreateDefaultBuilder(args)
                .ConfigureLogging(builder =>
                {
                    builder.AddConfiguration(config);
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.Configure<Application>(config.GetSection(typeof(Application).Name));
                    services.Configure<Provider>(config.GetSection(typeof(Provider).Name));
                    services.AddSingleton<NetworkListener>();
                    services.AddSingleton<TrafficMonitor>();

                    services.AddHostedService<Worker>();
                });
        }
    }
}
