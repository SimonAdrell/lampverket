using Lampverket.HomeAssistant.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;

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

        // Register the HA activity source with whatever OTel pipeline the host has wired up.
        // Additive and idempotent alongside AddServiceDefaults().ConfigureOpenTelemetry().
        services.AddOpenTelemetry()
            .WithTracing(t => t.AddSource(McpGateway.ActivitySourceName));

        // Readiness check: /api/ with Bearer token — confirms reachability + auth validity.
        // Tagged "ready" + "external" so it maps to the /health readiness endpoint, not /alive.
        services.AddHealthChecks()
            .AddCheck<HomeAssistantHealthCheck>("homeassistant", tags: ["ready", "external"]);

        services.AddSingleton<IMcpGateway, McpGateway>();
        // HomeAssistantClient is stateless — singleton avoids per-request allocation.
        services.AddSingleton<IHomeAssistantClient, HomeAssistantClient>();
        return services;
    }
}
