using Lampverket.HomeAssistant.Models;
using Lampverket.HomeAssistant.Options;
using Microsoft.Extensions.Options;

namespace Lampverket.HomeAssistant;

public sealed class HomeAssistantClient : IHomeAssistantClient
{
    private readonly IMcpGateway _gateway;
    private readonly HomeAssistantOptions _options;

    public HomeAssistantClient(IMcpGateway gateway, IOptions<HomeAssistantOptions> options)
    {
        _gateway = gateway;
        _options = options.Value;
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns the current state of a device from Home Assistant.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="device"/> is not present in the device map.
    /// Unlike the action methods (which return <see cref="HaResult.DeviceNotFound"/>),
    /// this method throws because <see cref="DeviceState"/> has no failure variant.
    /// </exception>
    public async Task<DeviceState> GetStateAsync(string device, CancellationToken ct = default)
    {
        var entry = FindDevice(device)
            ?? throw new ArgumentException($"Device '{device}' not in device map.", nameof(device));

        return await GetStateForEntryAsync(entry, ct);
    }

    public Task<HaResult> TurnOnAsync(string device, CancellationToken ct = default)
    {
        var entry = FindDevice(device);
        if (entry is null) return Task.FromResult<HaResult>(new HaResult.DeviceNotFound(device));
        return CallActionAsync(_options.Tools.TurnOn,
            new Dictionary<string, object?> { ["name"] = entry.EntityId },
            entry, ct);
    }

    public Task<HaResult> TurnOffAsync(string device, CancellationToken ct = default)
    {
        var entry = FindDevice(device);
        if (entry is null) return Task.FromResult<HaResult>(new HaResult.DeviceNotFound(device));
        return CallActionAsync(_options.Tools.TurnOff,
            new Dictionary<string, object?> { ["name"] = entry.EntityId },
            entry, ct);
    }

    public Task<HaResult> SetBrightnessAsync(string device, int percent, CancellationToken ct = default)
    {
        var entry = FindDevice(device);
        if (entry is null) return Task.FromResult<HaResult>(new HaResult.DeviceNotFound(device));
        var clamped = Math.Clamp(percent, 0, 100);
        return CallActionAsync(_options.Tools.LightSet,
            new Dictionary<string, object?> { ["name"] = entry.EntityId, ["brightness"] = clamped },
            entry, ct);
    }

    public Task<HaResult> SetVolumeAsync(string device, int percent, CancellationToken ct = default)
    {
        var entry = FindDevice(device);
        if (entry is null) return Task.FromResult<HaResult>(new HaResult.DeviceNotFound(device));
        var clamped = Math.Clamp(percent, 0, 100);
        return CallActionAsync(_options.Tools.SetVolume,
            new Dictionary<string, object?> { ["name"] = entry.EntityId, ["volume_level"] = clamped },
            entry, ct);
    }

    public Task<HaResult> PlayMediaAsync(string device, string query, CancellationToken ct = default)
    {
        var entry = FindDevice(device);
        if (entry is null) return Task.FromResult<HaResult>(new HaResult.DeviceNotFound(device));
        return CallActionAsync(_options.Tools.MediaSearchAndPlay,
            new Dictionary<string, object?> { ["name"] = entry.EntityId, ["search_query"] = query },
            entry, ct);
    }

    public Task<IReadOnlyList<McpToolInfo>> ListToolsAsync(CancellationToken ct = default) =>
        _gateway.ListToolsAsync(ct);

    // -----------------------------------------------------------------------
    // Internals
    // -----------------------------------------------------------------------

    private DeviceMapEntry? FindDevice(string device) =>
        _options.Devices.FirstOrDefault(d =>
            string.Equals(d.Friendly, device, StringComparison.OrdinalIgnoreCase));

    private async Task<HaResult> CallActionAsync(
        string toolName, Dictionary<string, object?> args, DeviceMapEntry entry, CancellationToken ct)
    {
        // Pre-check: avoid calling HA on unavailable/unknown devices.
        // Pass the already-resolved entry directly to skip a second FindDevice lookup.
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

    // -----------------------------------------------------------------------
    // GetLiveContext response parser
    // Real fixture (captured from live HA instance):
    //   - names: Banan
    //     domain: light
    //     state: 'on'
    //     areas: Bedroom
    //     attributes:
    //       brightness: '153'    ← READ brightness is 0-255, not percent
    // When off, brightness line is present but empty.
    // -----------------------------------------------------------------------

    private static DeviceState ParseLiveContextResponse(string content, DeviceMapEntry entry)
    {
        var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        bool inBlock = false;
        string? state = null;
        string? brightness = null;

        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();

            if (trimmed.StartsWith("- names:"))
            {
                var name = trimmed["- names:".Length..].Trim();
                inBlock = string.Equals(name, entry.Friendly, StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!inBlock) continue;

            if (trimmed.StartsWith("state:"))
                state = trimmed["state:".Length..].Trim().Trim('\'');
            else if (trimmed.StartsWith("brightness:"))
            {
                var val = trimmed["brightness:".Length..].Trim().Trim('\'');
                brightness = string.IsNullOrEmpty(val) ? null : val;
            }
        }

        // Allowlist: only "on" and "off" are treated as available.
        // null (device absent from response), "unavailable", "unknown" → not available.
        bool isAvailable = state is "on" or "off";
        bool isOn = state == "on";
        // Brightness on READ is 0-255; convert to 0-100 percent.
        int? brightnessPercent = brightness is not null && int.TryParse(brightness, out var raw)
            ? (int)Math.Round(raw / 255.0 * 100)
            : null;

        return new DeviceState(entry.Friendly, entry.EntityId, isOn, brightnessPercent, isAvailable);
    }
}
