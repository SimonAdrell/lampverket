using Lampverket.HomeAssistant.Models;
using Lampverket.HomeAssistant.Options;
using ExtOptions = Microsoft.Extensions.Options.Options;

namespace Lampverket.HomeAssistant.Tests;

public class HomeAssistantClientTests
{
    // -----------------------------------------------------------------------
    // Real GetLiveContext response fixtures (captured from live HA instance)
    // -----------------------------------------------------------------------

    private const string OnFixture = """
        Live Context: An overview of the areas and the devices in this smart home:
        - names: Banan
          domain: light
          state: 'on'
          areas: Bedroom
          attributes:
            brightness: '153'
        """;

    private const string OffFixture = """
        Live Context: An overview of the areas and the devices in this smart home:
        - names: Banan
          domain: light
          state: 'off'
          areas: Bedroom
          attributes:
            brightness:
        """;

    private const string UnavailableFixture = """
        Live Context: An overview of the areas and the devices in this smart home:
        - names: Banan
          domain: light
          state: 'unavailable'
          areas: Bedroom
          attributes:
            brightness:
        """;

    // -----------------------------------------------------------------------
    // Test helpers
    // -----------------------------------------------------------------------

    private static readonly DeviceMapEntry[] TestDevices =
    [
        new() { Friendly = "Banan", EntityId = "light.banan", Area = "Bedroom", Actions = ["on", "off", "brightness"] }
    ];

    private static HomeAssistantOptions TestOptions(DeviceMapEntry[]? devices = null) => new()
    {
        Devices = devices ?? TestDevices
    };

    private static HomeAssistantClient CreateSut(FakeMcpGateway gateway, HomeAssistantOptions? opts = null) =>
        new(gateway, ExtOptions.Create(opts ?? TestOptions()));

    // Configures the fake so GetLiveContext returns an available (on) device.
    private static void SetupAvailable(FakeMcpGateway fake) =>
        fake.Returns("GetLiveContext", new McpCallResult(false, OnFixture));

    // -----------------------------------------------------------------------
    // #1 — Unknown device name → DeviceNotFound
    // -----------------------------------------------------------------------

    [Fact]
    public async Task TurnOnAsync_UnknownDevice_ReturnsDeviceNotFound()
    {
        var fake = new FakeMcpGateway();
        var sut = CreateSut(fake);

        var result = await sut.TurnOnAsync("Lampa som inte finns");

        Assert.IsType<HaResult.DeviceNotFound>(result);
    }

    // -----------------------------------------------------------------------
    // #1b — Known device resolves entity_id from device map
    // -----------------------------------------------------------------------

    [Fact]
    public async Task TurnOnAsync_KnownDevice_PassesEntityIdToGateway()
    {
        var fake = new FakeMcpGateway();
        SetupAvailable(fake);
        var sut = CreateSut(fake);

        await sut.TurnOnAsync("Banan");

        var turnOnCall = fake.Calls.FirstOrDefault(c => c.ToolName == "HassTurnOn");
        Assert.NotNull(turnOnCall.ToolName);
        Assert.Equal("light.banan", turnOnCall.Args["name"]);
    }

    // -----------------------------------------------------------------------
    // #2 — TurnOnAsync → calls HassTurnOn, returns Ok
    // -----------------------------------------------------------------------

    [Fact]
    public async Task TurnOnAsync_KnownDevice_ReturnsOk()
    {
        var fake = new FakeMcpGateway();
        SetupAvailable(fake);
        var sut = CreateSut(fake);

        var result = await sut.TurnOnAsync("Banan");

        Assert.IsType<HaResult.Ok>(result);
    }

    [Fact]
    public async Task TurnOnAsync_KnownDevice_CallsHassTurnOnTool()
    {
        var fake = new FakeMcpGateway();
        SetupAvailable(fake);
        var sut = CreateSut(fake);

        await sut.TurnOnAsync("Banan");

        Assert.Contains(fake.Calls, c => c.ToolName == "HassTurnOn");
    }

    // -----------------------------------------------------------------------
    // #3 — TurnOffAsync → calls HassTurnOff, returns Ok
    // -----------------------------------------------------------------------

    [Fact]
    public async Task TurnOffAsync_UnknownDevice_ReturnsDeviceNotFound()
    {
        var fake = new FakeMcpGateway();
        var sut = CreateSut(fake);

        var result = await sut.TurnOffAsync("Lampa som inte finns");

        Assert.IsType<HaResult.DeviceNotFound>(result);
    }

    [Fact]
    public async Task TurnOffAsync_KnownDevice_ReturnsOk()
    {
        var fake = new FakeMcpGateway();
        SetupAvailable(fake);
        var sut = CreateSut(fake);

        var result = await sut.TurnOffAsync("Banan");

        Assert.IsType<HaResult.Ok>(result);
    }

    [Fact]
    public async Task TurnOffAsync_KnownDevice_CallsHassTurnOffTool()
    {
        var fake = new FakeMcpGateway();
        SetupAvailable(fake);
        var sut = CreateSut(fake);

        await sut.TurnOffAsync("Banan");

        Assert.Contains(fake.Calls, c => c.ToolName == "HassTurnOff");
    }
}
