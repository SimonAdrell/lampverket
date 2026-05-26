namespace Lampverket.HomeAssistant.Options;

public class HomeAssistantOptions
{
    public string BaseUrl { get; set; } = "";
    public string Token { get; set; } = "";
    /// <summary>
    /// Path to HA's MCP Server endpoint, relative to BaseUrl.
    /// Local HA: "/mcp"  |  Nabu Casa cloud: "/api/mcp"
    /// </summary>
    public string McpEndpointPath { get; set; } = "/mcp";
    public DeviceMapEntry[] Devices { get; set; } = [];
    public HaToolNames Tools { get; set; } = new();
}
