using System.Diagnostics;
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
    /// <summary>OTel activity source name — registered by <see cref="ServiceCollectionExtensions.AddHomeAssistant"/>.</summary>
    public const string ActivitySourceName = "Lampverket.HomeAssistant";

    private static readonly ActivitySource _activitySource = new(ActivitySourceName);

    private readonly HomeAssistantOptions _options;
    private readonly ILogger<McpGateway> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private volatile McpClient? _client;
    private int _disposed;

    public McpGateway(IOptions<HomeAssistantOptions> options, ILogger<McpGateway> logger, IHttpClientFactory httpClientFactory)
    {
        _options = options.Value;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IReadOnlyList<McpToolInfo>> ListToolsAsync(CancellationToken ct = default)
    {
        using var activity = _activitySource.StartActivity("mcp.ListTools");
        var client = await GetClientAsync(ct);
        try
        {
            var tools = await client.ListToolsAsync(cancellationToken: ct);
            return tools.Select(t => new McpToolInfo(t.Name, t.Description)).ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "MCP ListTools failed; invalidating client for reconnect.");
            InvalidateClient();
            throw;
        }
    }

    public async Task<McpCallResult> CallToolAsync(string toolName, IReadOnlyDictionary<string, object?> args, CancellationToken ct = default)
    {
        using var activity = _activitySource.StartActivity($"mcp.tool/{toolName}");
        activity?.SetTag("mcp.tool.name", toolName);

        var client = await GetClientAsync(ct);
        var argDict = args as Dictionary<string, object?> ?? new Dictionary<string, object?>(args);
        try
        {
            var result = await client.CallToolAsync(toolName, argDict, cancellationToken: ct);
            // MCP results are a list of content blocks; flatten all text blocks to one string.
            var text = string.Join("\n", result.Content
                .OfType<TextContentBlock>()
                .Select(b => b.Text));

            if (result.IsError ?? false)
                activity?.SetStatus(ActivityStatusCode.Error, text);

            return new McpCallResult(result.IsError ?? false, text);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "MCP tool {ToolName} failed; invalidating client for reconnect.", toolName);
            InvalidateClient();
            throw;
        }
    }

    /// <summary>
    /// Clears the cached <see cref="McpClient"/> so the next call triggers a fresh connection.
    /// Called on any transport-level exception. The old client is disposed best-effort;
    /// if it only implements <see cref="IAsyncDisposable"/>, the GC handles final cleanup.
    /// </summary>
    private void InvalidateClient()
    {
        var old = _client;
        _client = null;          // volatile write — immediately visible to other threads
        (old as IDisposable)?.Dispose();
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

        // Factory manages the client lifetime and socket pooling; ownsHttpClient: false.
        var httpClient = _httpClientFactory.CreateClient("HomeAssistant");
        var transportOptions = new HttpClientTransportOptions { Endpoint = endpointUri };
        var transport = new HttpClientTransport(transportOptions, httpClient, null, ownsHttpClient: false);

        return await McpClient.CreateAsync(transport, cancellationToken: ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        if (_client is IAsyncDisposable d) await d.DisposeAsync();
        _lock.Dispose();
    }

    // Sync fallback for DI containers that call Dispose() rather than DisposeAsync().
    // ASP.NET Core DI prefers IAsyncDisposable when both are implemented, so this path
    // is a last-resort safety net. Avoid blocking on async I/O here — deadlock risk.
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        (_client as IDisposable)?.Dispose();
        _lock.Dispose();
    }
}
