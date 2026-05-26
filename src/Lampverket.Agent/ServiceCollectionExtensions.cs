namespace Lampverket.Agent;

using Anthropic;
using Lampverket.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgent(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IAnthropicClient>(_ => new AnthropicClient
        {
            ApiKey = configuration["Anthropic:ApiKey"] ?? ""
        });

        services.AddSingleton<IClaudeClient, AnthropicClaudeClient>();
        services.AddSingleton<IHandlaggareService, HandlaggareService>();

        return services;
    }
}
