namespace Lampverket.HomeAssistant.Models;

public abstract record HaResult
{
    public sealed record Ok() : HaResult;
    public sealed record DeviceNotFound(string Name) : HaResult;
    public sealed record DeviceUnavailable(string Name) : HaResult;
    public sealed record ToolError(string Message) : HaResult;
}
