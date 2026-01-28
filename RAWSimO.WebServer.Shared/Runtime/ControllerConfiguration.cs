using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace RAWSimO.WebServer.Shared.Runtime;

public static class ControllerConfiguration
{
    // ReSharper disable once InconsistentNaming
    public static void AddRAWSimOWebServerControllers(this IServiceCollection services)
    {
        services.AddControllers().AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
            options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
        });
    }
}
