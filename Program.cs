using System.Runtime.Versioning;

namespace LdapMonitoring;

[SupportedOSPlatform("windows")]
internal static class Program
{
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.AddEventLog(settings =>
        {
            settings.SourceName = "ADUCMonitor";
        });

        var loggerFactory = LoggerFactory.Create(logging =>
        {
            logging.AddConsole();
            logging.AddEventLog();
        });

        var logger = loggerFactory.CreateLogger("Global");

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var exception = (Exception)e.ExceptionObject;
            logger.LogCritical(exception, "[FATAL] Unhandled exception");
        };

        builder.Services.AddHostedService<Worker>();

        var host = builder.Build();
        host.Run();
    }
}