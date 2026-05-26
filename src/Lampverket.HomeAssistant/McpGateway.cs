using Lampverket.HomeAssistant.Models;
using Lampverket.HomeAssistant.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Lampverket.HomeAssistant;

// Only place that references ModelContextProtocol SDK.
// Singleton — lazy-initialises one McpClient; reconnects on transport close.
public sealed class McpGateway : IMcpGateway, IAsyncDisposable, IDisposable
{
    private readonly HomeAssistantOptions _options;
    private readonly ILogger<McpGateway> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private volatile McpClient? _client;

    public McpGateway(IOptions<HomeAssistantOptions> options, ILogger<McpGateway> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<McpToolInfo>> ListToolsAsync(CancellationToken ct = default)
    {
        var client = await GetClientAsync(ct);
        var tools = await client.ListToolsAsync(cancellationToken: ct);
        return tools.Select(t => new McpToolInfo(t.Name, t.Description)).ToList();
    }

    public async Task<McpCallResult> CallToolAsync(string toolName, Dictionary<string, object?> args, CancellationToken ct = default)
    {
        var client = await GetClientAsync(ct);
        var result = await client.CallToolAsync(toolName, args, cancellationToken: ct);
        // MCP results are a list of content blocks; flatten all text blocks to one string.
        var text = string.Join("\n", result.Content
            .OfType<TextContentBlock>()
            .Select(b => b.Text));
        return new McpCallResult(result.IsError ?? false, text);
    }

    private async Task<McpClient> GetClientAsync(CancellationToken ct)
    {
        if (_client is not null) return _client;

        await _lock.WaitAsync(ct);
        try
        {
            if (_client is not null) return _client;
            _client = await CreateClientAsync(ct);
            return _client;
        }
        finally { _lock.Release(); }
    }

    private async Task<McpClient> CreateClientAsync(CancellationToken ct)
    {
        var endpointUri = new Uri(_options.BaseUrl.TrimEnd('/') + _options.McpEndpointPath);
        _logger.LogInformation("Connecting to HA MCP Server at {Endpoint}", endpointUri);

        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.Token);

        var transportOptions = new HttpClientTransportOptions { Endpoint = endpointUri };
        var transport = new HttpClientTransport(transportOptions, httpClient, null, ownsHttpClient: true);

        return await McpClient.CreateAsync(transport, cancellationToken: ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is IAsyncDisposable d) await d.DisposeAsync();
        _lock.Dispose();
    }

    // Sync fallback for DI containers that call Dispose() rather than DisposeAsync().
    // ASP.NET Core DI prefers IAsyncDisposable when both are implemented, so this path
    // is a last-resort safety net. Avoid blocking on async I/O here — deadlock risk.
    public void Dispose()
    {
        (_client as IDisposable)?.Dispose();
        _lock.Dispose();
    }
}
