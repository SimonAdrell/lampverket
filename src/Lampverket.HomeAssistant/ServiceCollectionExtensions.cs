using Lampverket.HomeAssistant.Options;
using Microsoft.Extensions.DependencyInjection;

namespace Lampverket.HomeAssistant;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHomeAssistant(this IServiceCollection services)
    {
        services.AddSingleton<IMcpGateway, McpGateway>();
        services.AddScoped<IHomeAssistantClient, HomeAssistantClient>();
        return services;
    }
}
