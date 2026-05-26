using Lampverket.HomeAssistant.Models;

namespace Lampverket.HomeAssistant;

public interface IMcpGateway
{
    Task<IReadOnlyList<McpToolInfo>> ListToolsAsync(CancellationToken ct = default);
    Task<McpCallResult> CallToolAsync(string toolName, IReadOnlyDictionary<string, object?> args, CancellationToken ct = default);
}
