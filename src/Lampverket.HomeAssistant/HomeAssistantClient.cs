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

    public Task<DeviceState> GetStateAsync(string device, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<HaResult> TurnOnAsync(string device, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<HaResult> TurnOffAsync(string device, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<HaResult> SetBrightnessAsync(string device, int percent, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<HaResult> SetVolumeAsync(string device, int percent, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<HaResult> PlayMediaAsync(string device, string query, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<McpToolInfo>> ListToolsAsync(CancellationToken ct = default) =>
        throw new NotImplementedException();
}
