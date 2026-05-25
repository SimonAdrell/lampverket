using Lampverket.HomeAssistant.Models;
using Lampverket.HomeAssistant.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lampverket.HomeAssistant;

// Real adapter — wraps ModelContextProtocol SDK. Added after the addressing spike confirms the SDK API.
public sealed class McpGateway : IMcpGateway, IAsyncDisposable
{
    private readonly HomeAssistantOptions _options;
    private readonly ILogger<McpGateway> _logger;

    public McpGateway(IOptions<HomeAssistantOptions> options, ILogger<McpGateway> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<IReadOnlyList<McpToolInfo>> ListToolsAsync(CancellationToken ct = default) =>
        throw new NotImplementedException("McpGateway is not yet implemented — see Step 1 of the build plan.");

    public Task<McpCallResult> CallToolAsync(string toolName, Dictionary<string, object?> args, CancellationToken ct = default) =>
        throw new NotImplementedException("McpGateway is not yet implemented — see Step 1 of the build plan.");

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
