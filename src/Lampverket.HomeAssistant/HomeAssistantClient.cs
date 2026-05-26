using Lampverket.HomeAssistant.Models;
using Lampverket.HomeAssistant.Options;
using Microsoft.Extensions.Options;

namespace Lampverket.HomeAssistant;

public sealed class HomeAssistantClient(IMcpGateway gateway, IOptions<HomeAssistantOptions> options) : IHomeAssistantClient
{
    private readonly IMcpGateway _gateway = gateway;
    private readonly HomeAssistantOptions _options = options.Value;

    /// <summary>
    /// Returns the current state of a device from Home Assistant.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="device"/> is not present in the appsettings device map.
    /// Unlike the action methods (which return <see cref="HaResult.DeviceNotFound"/>),
    /// this method throws because <see cref="DeviceState"/> has no failure variant.
    /// </exception>
    public async Task<DeviceState> GetStateAsync(string device, CancellationToken ct = default)
    {
        var entry = FindDevice(device)
            ?? throw new ArgumentException($"Device '{device}' not in device map.", nameof(device));

        return await GetStateForEntryAsync(entry, ct);
    }

    public async Task<HaResult> TurnOnAsync(string device, CancellationToken ct = default)
    {
        var entry = FindDevice(device);
        if (entry is null) return new HaResult.DeviceNotFound(device);
        return await CallActionAsync(_options.Tools.TurnOn,
            new Dictionary<string, object?> { ["name"] = entry.EntityId },
            entry, ct);
    }

    public async Task<HaResult> TurnOffAsync(string device, CancellationToken ct = default)
    {
        var entry = FindDevice(device);
        if (entry is null) return new HaResult.DeviceNotFound(device);
        return await CallActionAsync(_options.Tools.TurnOff,
            new Dictionary<string, object?> { ["name"] = entry.EntityId },
            entry, ct);
    }

    public async Task<HaResult> SetBrightnessAsync(string device, int percent, CancellationToken ct = default)
    {
        var entry = FindDevice(device);
        if (entry is null) return new HaResult.DeviceNotFound(device);
        var clamped = Math.Clamp(percent, 0, 100);
        return await CallActionAsync(_options.Tools.LightSet,
            new Dictionary<string, object?> { ["name"] = entry.EntityId, ["brightness"] = clamped },
            entry, ct);
    }

    public async Task<HaResult> SetVolumeAsync(string device, int percent, CancellationToken ct = default)
    {
        var entry = FindDevice(device);
        if (entry is null) return new HaResult.DeviceNotFound(device);
        var clamped = Math.Clamp(percent, 0, 100);
        return await CallActionAsync(_options.Tools.SetVolume,
            new Dictionary<string, object?> { ["name"] = entry.EntityId, ["volume_level"] = clamped },
            entry, ct);
    }

    public async Task<HaResult> PlayMediaAsync(string device, string query, CancellationToken ct = default)
    {
        var entry = FindDevice(device);
        if (entry is null) return new HaResult.DeviceNotFound(device);
        return await CallActionAsync(_options.Tools.MediaSearchAndPlay,
            new Dictionary<string, object?> { ["name"] = entry.EntityId, ["search_query"] = query },
            entry, ct);
    }

    public Task<IReadOnlyList<McpToolInfo>> ListToolsAsync(CancellationToken ct = default) =>
        _gateway.ListToolsAsync(ct);

    private DeviceMapEntry? FindDevice(string device) =>
        _options.Devices.FirstOrDefault(d =>
            string.Equals(d.Friendly, device, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(d.EntityId, device, StringComparison.OrdinalIgnoreCase));

    private async Task<HaResult> CallActionAsync(
        string toolName, Dictionary<string, object?> args, DeviceMapEntry entry, CancellationToken ct)
    {
        var state = await GetStateForEntryAsync(entry, ct);
        if (!state.IsAvailable) return new HaResult.DeviceUnavailable(entry.Friendly);

        var result = await _gateway.CallToolAsync(toolName, args, ct);
        return result.IsError ? new HaResult.ToolError(result.Content) : new HaResult.Ok();
    }

    private async Task<DeviceState> GetStateForEntryAsync(DeviceMapEntry entry, CancellationToken ct)
    {
        var result = await _gateway.CallToolAsync(
            _options.Tools.GetLiveContext,
            new Dictionary<string, object?> { ["name"] = entry.EntityId },
            ct);
        return ParseLiveContextResponse(result.Content, entry);
    }

    private static DeviceState ParseLiveContextResponse(string content, DeviceMapEntry entry)
    {
        var lines = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        bool inBlock = false;
        string? state = null;
        string? brightness = null;

        foreach (var line in lines)
        {
            switch (line.TrimStart())
            {
                case var s when s.StartsWith("- names:"):
                    inBlock = string.Equals(s["- names:".Length..].Trim(), entry.Friendly, StringComparison.OrdinalIgnoreCase);
                    break;
                case var s when inBlock && s.StartsWith("state:"):
                    state = s["state:".Length..].Trim().Trim('\'');
                    break;
                case var s when inBlock && s.StartsWith("brightness:"):
                    {
                        var val = s["brightness:".Length..].Trim().Trim('\'');
                        brightness = string.IsNullOrEmpty(val) ? null : val;
                        break;
                    }
            }
        }

        bool isAvailable = state is "on" or "off";
        bool isOn = state is "on";
        int? brightnessPercent = brightness is not null && int.TryParse(brightness, out var raw)
            ? (int)Math.Round(raw / 255.0 * 100)
            : null;

        return new DeviceState(entry.Friendly, entry.EntityId, isOn, brightnessPercent, isAvailable);
    }
}
