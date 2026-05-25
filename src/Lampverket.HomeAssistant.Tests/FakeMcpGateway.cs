using Lampverket.HomeAssistant.Models;

namespace Lampverket.HomeAssistant.Tests;

public sealed class FakeMcpGateway : IMcpGateway
{
    private readonly Dictionary<string, McpCallResult> _perToolResults = new();
    private McpCallResult _defaultResult = new(false, "Done.");

    public List<(string ToolName, Dictionary<string, object?> Args)> Calls { get; } = [];
    public IReadOnlyList<McpToolInfo> ToolList { get; set; } = [];

    /// <summary>Sets the result returned when <paramref name="toolName"/> is called.</summary>
    public void Returns(string toolName, McpCallResult result) =>
        _perToolResults[toolName] = result;

    /// <summary>Sets the result returned for any tool not explicitly configured.</summary>
    public void ReturnsDefault(McpCallResult result) => _defaultResult = result;

    public Task<IReadOnlyList<McpToolInfo>> ListToolsAsync(CancellationToken ct = default) =>
        Task.FromResult(ToolList);

    public Task<McpCallResult> CallToolAsync(string toolName, Dictionary<string, object?> args, CancellationToken ct = default)
    {
        Calls.Add((toolName, new Dictionary<string, object?>(args)));
        var result = _perToolResults.TryGetValue(toolName, out var r) ? r : _defaultResult;
        return Task.FromResult(result);
    }
}
