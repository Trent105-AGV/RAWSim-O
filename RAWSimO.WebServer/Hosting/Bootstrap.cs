using System.Reflection;
using System.Text;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Mvc;
using RAWSimO.WebServer.Health;
using RAWSimO.WebServer.Hubs;
using RAWSimO.WebServer.Shared.Configuration;
using RAWSimO.WebServer.Shared.Runtime;
using RAWSimO.WebServer.SimulationHost;

namespace RAWSimO.WebServer.Hosting;

public static class Bootstrap
{
    public static void Run(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.WebHost.UseKestrelHttpsConfiguration();

        // 启用依赖注入验证
        builder.Host.UseDefaultServiceProvider((_, options) =>
        {
            options.ValidateScopes = true;
            options.ValidateOnBuild = true;
        });

        builder.Services.AddRAWSimOWebServerLogging();
        builder.Services.AddMagicOnion();

        builder.Services.AddSingleton<GrpcChannel>(_ =>
        {
            var serviceConfig = builder.Configuration.GetSection("Services").Get<ServiceConfig>() ??
                                new ServiceConfig();
            var mirBeServiceUrl = serviceConfig.MirBe;
            var httpClientHandler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            };

            var channelOptions = new GrpcChannelOptions
            {
                HttpHandler = httpClientHandler
            };

            var channel = GrpcChannel.ForAddress(mirBeServiceUrl, channelOptions);
            return channel;
        });

        builder.Services.AddRAWSimOWebServerControllers();
        builder.Services.AddSimulationHostModule();
        builder.Services.AddHealthModule();

        builder.Services.AddSignalR(options => { options.EnableDetailedErrors = true; });

        // 配置 CORS
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        Console.WriteLine("Building application with DI validation enabled...");
        var app = builder.Build();
        Console.WriteLine("✓ DI container built successfully");

        ValidateControllers(app.Services);

        // 配置路由（必须在认证/授权之前）
        app.UseRouting();

        // 启用 CORS
        app.UseCors();

        app.UseSimulationHostModule();
        app.UseHealthModule();

        // 映射端点（必须在认证/授权之后）
        app.MapMagicOnionService();
        app.MapControllers();
        app.MapHub<MessageHub>("/hub");

        app.MapGet("/health", () => "healthy");

        app.Run();

    }

    private static void ValidateControllers(IServiceProvider services)
    {
        var builder = new StringBuilder();
        builder.AppendLine("\n========================================");
        builder.AppendLine("Validating Controllers...");
        builder.AppendLine("========================================");

        var controllerTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) &&
                        t is { IsAbstract: false, IsInterface: false })
            .OrderBy(t => t.Name)
            .ToList();

        builder.AppendLine($"Found {controllerTypes.Count} controllers\n");

        var failedControllers = new List<(Type Type, Exception Exception)>();

        foreach (var controllerType in controllerTypes)
        {
            try
            {
                using var scope = services.CreateScope();
                ActivatorUtilities.CreateInstance(scope.ServiceProvider, controllerType);
                builder.AppendLine($"  ✓ {controllerType.Name}");
            }
            catch (Exception ex)
            {
                builder.AppendLine($"  ✗ {controllerType.Name}");
                builder.AppendLine($"     Error: {ex.Message}");
                failedControllers.Add((controllerType, ex));
            }
        }

        if (failedControllers.Count != 0)
        {
            builder.AppendLine("\n========================================");
            builder.AppendLine($"✗ {failedControllers.Count} controller(s) failed validation");
            builder.AppendLine("========================================\n");

            foreach (var (type, exception) in failedControllers)
            {
                builder.AppendLine($"Controller: {type.FullName}");
                builder.AppendLine($"Error: {exception.Message}");

                var innerEx = exception.InnerException;
                while (innerEx != null)
                {
                    builder.AppendLine($"  → {innerEx.Message}");
                    innerEx = innerEx.InnerException;
                }

                builder.AppendLine();
            }

            throw new InvalidOperationException(
                $"{failedControllers.Count} controller(s) failed dependency validation. " +
                "Please check if all required services are registered in the DI container.");
        }

        builder.AppendLine($"\n✓ All {controllerTypes.Count} controllers validated successfully");
        builder.AppendLine("========================================\n");

        Console.WriteLine(builder.ToString());
    }
}