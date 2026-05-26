using Lampverket.HomeAssistant.Models;

namespace Lampverket.HomeAssistant;

public interface IHomeAssistantClient
{
    Task<DeviceState> GetStateAsync(string device, CancellationToken ct = default);
    Task<HaResult> TurnOnAsync(string device, CancellationToken ct = default);
    Task<HaResult> TurnOffAsync(string device, CancellationToken ct = default);
    Task<HaResult> SetBrightnessAsync(string device, int percent, CancellationToken ct = default);
    Task<HaResult> SetVolumeAsync(string device, int percent, CancellationToken ct = default);
    Task<HaResult> PlayMediaAsync(string device, string query, CancellationToken ct = default);
    Task<IReadOnlyList<McpToolInfo>> ListToolsAsync(CancellationToken ct = default);
}
