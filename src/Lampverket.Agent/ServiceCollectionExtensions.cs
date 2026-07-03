using Anthropic;
using System.Threading.Channels;
using Lampverket.Agent.HomeAssistant;
using Lampverket.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Lampverket.Agent;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgent(this IServiceCollection services, IConfiguration configuration)
    {
        // Validate HA secrets at startup so the agent fails fast if the MCP target is misconfigured.
        services.AddOptions<HomeAssistantOptions>()
            .BindConfiguration("HomeAssistant")
            .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "HomeAssistant:BaseUrl is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Token), "HomeAssistant:Token is required.")
            .ValidateOnStart();

        services.AddHttpClient("HomeAssistant")
            .ConfigureHttpClient((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<HomeAssistantOptions>>().Value;
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opts.Token);
            });

        services.AddHealthChecks()
            .AddCheck<HomeAssistantHealthCheck>("homeassistant", tags: ["ready", "external"]);

        var apiKey = configuration["Anthropic:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Anthropic:ApiKey is required.");
        }

        services.AddSingleton<IAnthropicClient>(_ => new AnthropicClient
        {
            ApiKey = apiKey,
            BaseUrl = "https://api.anthropic.com"
        });

        services.AddSingleton<HaMcpProvider>();
        services.AddSingleton<HaToolFactory>();
        services.AddSingleton<IHandlaggareAgent, HandlaggareAgent>();
        services.AddSingleton<HandlaggareService>();
        services.AddSingleton<IAnsokanService>(sp => sp.GetRequiredService<HandlaggareService>());
        services.AddSingleton<IArendeProcessor>(sp => sp.GetRequiredService<HandlaggareService>());

        var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
        services.AddSingleton(channel.Writer);
        services.AddSingleton(channel.Reader);
        services.AddHostedService<HandlaggareBackgroundService>();

        return services;
    }
}
