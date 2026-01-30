using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace RAWSimO.WebServer.Shared.Runtime;

public static class LoggerConfigurationExtensions
{
    // ReSharper disable once InconsistentNaming
    public static void AddRAWSimOWebServerLogging(this IServiceCollection self)
    {
        const string consoleTemplate
            = "L [{Timestamp:MM/dd HH:mm:ss}] | {Level} | {SourceContext}{Scope} {MapName} {NewLine}{Message:lj}{NewLine}{Exception}{NewLine}";

        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .MinimumLevel.Verbose()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .MinimumLevel.Override("System.Net.Http", LogEventLevel.Fatal)
            .Filter.ByExcluding(logEvent =>
                logEvent.Properties.ContainsKey("SourceContext") &&
                (
                    logEvent.Properties["SourceContext"].ToString()
                        .Contains("Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker") ||
                    logEvent.Properties["SourceContext"].ToString()
                        .Contains("Microsoft.AspNetCore.Routing.EndpointMiddleware") ||
                    logEvent.Properties["SourceContext"].ToString()
                        .Contains("Microsoft.AspNetCore.Hosting.Diagnostics") ||
                    logEvent.Properties["SourceContext"].ToString()
                        .Contains("Microsoft.AspNetCore.Cors.Infrastructure.CorsService") ||
                    logEvent.Properties["SourceContext"].ToString()
                        .Contains("Microsoft.AspNetCore.Server.Kestrel.Connections") ||
                    logEvent.Properties["SourceContext"].ToString()
                        .Contains("Microsoft.AspNetCore.Mvc.Infrastructure.ObjectResultExecutor") ||
                    logEvent.Properties["SourceContext"].ToString()
                        .Contains("Microsoft.AspNetCore.DataProtection.Repositories.FileSystemXmlRepository") ||
                    logEvent.Properties["SourceContext"].ToString()
                        .Contains("Microsoft.AspNetCore.DataProtection.KeyManagement.XmlKeyManager")
                ))
            .WriteTo.Logger(lc =>
                lc.Filter.ByIncludingOnly(e => e.Level >= LogEventLevel.Information)
                    .WriteTo.Console(
                        theme: AnsiConsoleTheme.Code,
                        outputTemplate: consoleTemplate,
                        applyThemeToRedirectedOutput: false
                    )
            )
            .CreateLogger();

        self.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddSerilog();
            logging.SetMinimumLevel(LogLevel.Information);
        });
    }
}