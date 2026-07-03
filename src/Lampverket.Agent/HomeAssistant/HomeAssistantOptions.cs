namespace Lampverket.Agent.HomeAssistant;

public class HomeAssistantOptions
{
    public string BaseUrl { get; set; } = "";
    public string Token { get; set; } = "";
    public string McpEndpointPath { get; set; } = "/mcp";
    public DeviceMapEntry[] Devices { get; set; } = [];
}
