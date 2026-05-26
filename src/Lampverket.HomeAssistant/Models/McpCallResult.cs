namespace Lampverket.HomeAssistant.Models;

// MCP tool results are a list of content blocks; McpGateway flattens them to a single string.
public record McpCallResult(bool IsError, string Content);
