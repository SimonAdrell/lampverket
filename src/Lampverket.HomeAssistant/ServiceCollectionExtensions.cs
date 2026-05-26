using Lampverket.HomeAssistant.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Lampverket.HomeAssistant;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHomeAssistant(this IServiceCollection services)
    {
        // Validate required secrets at startup — fail fast before any HA call.
        services.AddOptions<HomeAssistantOptions>()
            .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl),  "HomeAssistant:BaseUrl is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Token),    "HomeAssistant:Token is required.")
            .ValidateOnStart();

        services.AddHttpClient("HomeAssistant")
            .ConfigureHttpClient((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<HomeAssistantOptions>>().Value;
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opts.Token);
            });

        services.AddSingleton<IMcpGateway, McpGateway>();
        // HomeAssistantClient is stateless — singleton avoids per-request allocation.
        services.AddSingleton<IHomeAssistantClient, HomeAssistantClient>();
        return services;
    }
}
