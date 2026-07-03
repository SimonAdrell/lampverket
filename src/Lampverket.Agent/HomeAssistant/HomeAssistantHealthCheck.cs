using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Lampverket.Agent.HomeAssistant;

/// <summary>
/// Readiness check: hits HA's REST API root (<c>/api/</c>) with the configured Bearer token.
/// A 200 response confirms the server is reachable and the token is valid.
/// </summary>
/// <remarks>
/// Uses a short-lived <see cref="HttpClient"/> directly rather than <see cref="IHttpClientFactory"/>
/// so the check bypasses the factory's resilience pipeline (retry + circuit breaker).
/// A health check against an unreachable host must fail fast, not retry.
/// </remarks>
internal sealed class HomeAssistantHealthCheck(IOptions<HomeAssistantOptions> opts) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext ctx, CancellationToken ct = default)
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opts.Value.Token);

            // Probe the MCP endpoint; GET returns 405 on HA cloud (POST-only), which still means reachable.
            var uri = new Uri(opts.Value.BaseUrl.TrimEnd('/') + opts.Value.McpEndpointPath);
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return HealthCheckResult.Unhealthy("Home Assistant token is invalid or expired.");

            // 405 = MCP endpoint exists but rejects GET (POST-only) — server is reachable.
            if (response.StatusCode == System.Net.HttpStatusCode.MethodNotAllowed)
                return HealthCheckResult.Healthy();

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
