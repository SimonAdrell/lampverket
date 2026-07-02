using Anthropic.Helpers.Beta;
using Anthropic.Helpers.Beta.Mcp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;

namespace Lampverket.Agent.HomeAssistant;

public sealed class HaMcpProvider : IAsyncDisposable
{
    private const string HaHttpClientName = "HomeAssistant";

    private readonly IHttpClientFactory _httpFactory;
    private readonly HomeAssistantOptions _haOptions;
    private readonly ILogger<HaMcpProvider> _logger;
    // Lazy<Task<T>> gives thread-safe one-shot async init; every caller awaits the same underlying
    // task and the result is cached. If MCP init fails the failed Task is cached — the agent is
    // unusable until the process restarts, which matches "HA is unreachable" reality.
    private readonly Lazy<Task<McpSetup>> _setup;

    public HaMcpProvider(
        IHttpClientFactory httpFactory,
        IOptions<HomeAssistantOptions> haOptions,
        ILogger<HaMcpProvider> logger)
    {
        _httpFactory = httpFactory;
        _haOptions = haOptions.Value;
        _logger = logger;
        _setup = new Lazy<Task<McpSetup>>(InitialiseAsync);
    }

    // Per-call cancellation is honoured via WaitAsync(ct); the underlying init runs to completion
    // without a token so a caller cancellation doesn't tear down the shared MCP client.
    public async Task<IReadOnlyList<IBetaRunnableTool>> GetToolsAsync(CancellationToken ct = default)
    {
        var setup = await _setup.Value.WaitAsync(ct);
        return setup.Tools;
    }

    private async Task<McpSetup> InitialiseAsync()
    {
        var endpoint = new Uri(_haOptions.BaseUrl.TrimEnd('/') + _haOptions.McpEndpointPath);
        var httpClient = _httpFactory.CreateClient(HaHttpClientName);
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions { Endpoint = endpoint },
            httpClient, null, ownsHttpClient: false);

        var client = await McpClient.CreateAsync(transport);
        var tools = await BetaMcp.ListToolsAsync(client);

        _logger.LogInformation("MCP client initialised: {Count} HA tool(s) loaded", tools.Count);
        return new McpSetup(client, tools);
    }

    public async ValueTask DisposeAsync()
    {
        if (_setup.IsValueCreated && _setup.Value.IsCompletedSuccessfully)
            await _setup.Value.Result.Client.DisposeAsync();
    }

    private sealed record McpSetup(McpClient Client, IReadOnlyList<IBetaRunnableTool> Tools);
}
