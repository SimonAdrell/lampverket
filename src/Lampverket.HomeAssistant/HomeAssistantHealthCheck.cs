using Lampverket.HomeAssistant.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Lampverket.HomeAssistant;

/// <summary>
/// Readiness check: hits HA's REST API root (<c>/api/</c>) with the configured Bearer token.
/// A 200 response confirms the server is reachable and the token is valid.
/// Tagged "ready" and "external" so it can be filtered separately from the liveness check.
/// </summary>
internal sealed class HomeAssistantHealthCheck(
    IHttpClientFactory factory,
    IOptions<HomeAssistantOptions> opts) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext ctx, CancellationToken ct = default)
    {
        try
        {
            var client = factory.CreateClient("HomeAssistant");
            var uri = new Uri(opts.Value.BaseUrl.TrimEnd('/') + "/api/");
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Degraded($"Home Assistant returned {(int)response.StatusCode} {response.ReasonPhrase}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Home Assistant is unreachable.", ex);
        }
    }
}
