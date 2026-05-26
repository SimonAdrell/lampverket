using Lampverket.HomeAssistant.Models;
using Lampverket.HomeAssistant.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Lampverket.HomeAssistant;

public sealed class McpGateway(IOptions<HomeAssistantOptions> options, ILogger<McpGateway> logger, IHttpClientFactory httpClientFactory) : IMcpGateway
{
    private readonly HomeAssistantOptions _options = options.Value;
    private readonly ILogger<McpGateway> _logger = logger;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

    public async Task<IReadOnlyList<McpToolInfo>> ListToolsAsync(CancellationToken ct = default)
    {
        await using var client = await CreateClientAsync(ct);
        var tools = await client.ListToolsAsync(cancellationToken: ct);
        return tools.Select(t => new McpToolInfo(t.Name, t.Description)).ToList();
    }

    public async Task<McpCallResult> CallToolAsync(string toolName, IReadOnlyDictionary<string, object?> args, CancellationToken ct = default)
    {
        await using var client = await CreateClientAsync(ct);
        var argDict = args as Dictionary<string, object?> ?? new Dictionary<string, object?>(args);
        var result = await client.CallToolAsync(toolName, argDict, cancellationToken: ct);
        var text = string.Join("\n", result.Content
            .OfType<TextContentBlock>()
            .Select(b => b.Text));
        return new McpCallResult(result.IsError ?? false, text);
    }

    private async Task<McpClient> CreateClientAsync(CancellationToken ct)
    {
        var endpointUri = new Uri(_options.BaseUrl.TrimEnd('/') + _options.McpEndpointPath);
        _logger.LogDebug("Connecting to HA MCP Server at {Endpoint}", endpointUri);

        var httpClient = _httpClientFactory.CreateClient("HomeAssistant");
        var transportOptions = new HttpClientTransportOptions { Endpoint = endpointUri };
        var transport = new HttpClientTransport(transportOptions, httpClient, null, ownsHttpClient: false);

        return await McpClient.CreateAsync(transport, cancellationToken: ct);
    }
}
