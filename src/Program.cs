using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RtcCallMonitor.Configuration;

namespace RtcCallMonitor
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        private static IConfiguration configuration;

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostContext, config) =>
                {
                    config.Sources.Clear();

                    var env = hostContext.HostingEnvironment;

                    config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                          .AddJsonFile($"appsettings.{env.EnvironmentName}.json",
                                         optional: true, reloadOnChange: true);

                    configuration = config.Build();
                })
                .ConfigureLogging(builder =>
                {
                    builder.AddConfiguration(configuration);
                    builder.AddSystemdConsole();

                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.Configure<Application>(configuration.GetSection(typeof(Application).Name));
                    services.Configure<Provider>(configuration.GetSection(typeof(Provider).Name));
                    services.AddSingleton<NetworkListener>();
                    services.AddSingleton<TrafficMonitor>();

                    services.AddHostedService<Worker>();
                })
                .UseWindowsService();
        }
    }
}
