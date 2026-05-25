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

    // -----------------------------------------------------------------------
    // #4a — SetBrightnessAsync passes percent as brightness arg (0-100 int)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SetBrightnessAsync_CallsHassLightSetWithBrightnessPercent()
    {
        var fake = new FakeMcpGateway();
        SetupAvailable(fake);
        var sut = CreateSut(fake);

        await sut.SetBrightnessAsync("Banan", 60);

        var call = fake.Calls.FirstOrDefault(c => c.ToolName == "HassLightSet");
        Assert.NotNull(call.ToolName);
        Assert.Equal(60, call.Args["brightness"]);
    }

    [Fact]
    public async Task SetBrightnessAsync_KnownDevice_ReturnsOk()
    {
        var fake = new FakeMcpGateway();
        SetupAvailable(fake);
        var sut = CreateSut(fake);

        var result = await sut.SetBrightnessAsync("Banan", 60);

        Assert.IsType<HaResult.Ok>(result);
    }

    // #4b — Clamping: < 0 → 0, > 100 → 100
    [Theory]
    [InlineData(-5, 0)]
    [InlineData(150, 100)]
    [InlineData(0, 0)]
    [InlineData(100, 100)]
    public async Task SetBrightnessAsync_ClampsPercentTo0to100(int input, int expected)
    {
        var fake = new FakeMcpGateway();
        SetupAvailable(fake);
        var sut = CreateSut(fake);

        await sut.SetBrightnessAsync("Banan", input);

        var call = fake.Calls.FirstOrDefault(c => c.ToolName == "HassLightSet");
        Assert.NotNull(call.ToolName);
        Assert.Equal(expected, call.Args["brightness"]);
    }

    // -----------------------------------------------------------------------
    // #5a — SetVolumeAsync passes percent as volume_level arg (0-100 int)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SetVolumeAsync_CallsHassSetVolumeWithVolumeLevel()
    {
        var fake = new FakeMcpGateway();
        SetupAvailable(fake);
        var sut = CreateSut(fake, TestOptions(
        [
            new() { Friendly = "Banan", EntityId = "media_player.banan", Area = "Bedroom", Actions = ["volume"] }
        ]));

        await sut.SetVolumeAsync("Banan", 40);

        var call = fake.Calls.FirstOrDefault(c => c.ToolName == "HassSetVolume");
        Assert.NotNull(call.ToolName);
        Assert.Equal(40, call.Args["volume_level"]);
    }

    [Fact]
    public async Task SetVolumeAsync_KnownDevice_ReturnsOk()
    {
        var fake = new FakeMcpGateway();
        SetupAvailable(fake);
        var sut = CreateSut(fake);

        var result = await sut.SetVolumeAsync("Banan", 40);

        Assert.IsType<HaResult.Ok>(result);
    }

    // #5b — Clamping: < 0 → 0, > 100 → 100
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(101, 100)]
    public async Task SetVolumeAsync_ClampsPercentTo0to100(int input, int expected)
    {
        var fake = new FakeMcpGateway();
        SetupAvailable(fake);
        var sut = CreateSut(fake);

        await sut.SetVolumeAsync("Banan", input);

        var call = fake.Calls.FirstOrDefault(c => c.ToolName == "HassSetVolume");
        Assert.NotNull(call.ToolName);
        Assert.Equal(expected, call.Args["volume_level"]);
    }

    // -----------------------------------------------------------------------
    // #6 — PlayMediaAsync passes search_query (not "query")
    // -----------------------------------------------------------------------

    [Fact]
    public async Task PlayMediaAsync_PassesSearchQueryArg()
    {
        var fake = new FakeMcpGateway();
        SetupAvailable(fake);
        var sut = CreateSut(fake);

        await sut.PlayMediaAsync("Banan", "jazz");

        var call = fake.Calls.FirstOrDefault(c => c.ToolName == "HassMediaSearchAndPlay");
        Assert.NotNull(call.ToolName);
        Assert.Equal("jazz", call.Args["search_query"]);
    }

    [Fact]
    public async Task PlayMediaAsync_KnownDevice_ReturnsOk()
    {
        var fake = new FakeMcpGateway();
        SetupAvailable(fake);
        var sut = CreateSut(fake);

        var result = await sut.PlayMediaAsync("Banan", "jazz");

        Assert.IsType<HaResult.Ok>(result);
    }

    // -----------------------------------------------------------------------
    // #7 — GetStateAsync parses on-state fixture correctly
    // brightness '153' → round(153/255*100) = 60
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetStateAsync_OnFixture_ParsesStateCorrectly()
    {
        var fake = new FakeMcpGateway();
        fake.Returns("GetLiveContext", new McpCallResult(false, OnFixture));
        var sut = CreateSut(fake);

        var state = await sut.GetStateAsync("Banan");

        Assert.Equal("Banan", state.FriendlyName);
        Assert.Equal("light.banan", state.EntityId);
        Assert.True(state.IsOn);
        Assert.Equal(60, state.BrightnessPercent);
        Assert.True(state.IsAvailable);
    }

    // -----------------------------------------------------------------------
    // #7b — GetStateAsync parses off-state fixture: IsOn=false, brightness null
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetStateAsync_OffFixture_ParsesStateCorrectly()
    {
        var fake = new FakeMcpGateway();
        fake.Returns("GetLiveContext", new McpCallResult(false, OffFixture));
        var sut = CreateSut(fake);

        var state = await sut.GetStateAsync("Banan");

        Assert.False(state.IsOn);
        Assert.Null(state.BrightnessPercent);
        Assert.True(state.IsAvailable);
    }

    // -----------------------------------------------------------------------
    // #8 — Unavailable device: TurnOnAsync returns DeviceUnavailable
    // -----------------------------------------------------------------------

    [Fact]
    public async Task TurnOnAsync_UnavailableDevice_ReturnsDeviceUnavailable()
    {
        var fake = new FakeMcpGateway();
        fake.Returns("GetLiveContext", new McpCallResult(false, UnavailableFixture));
        var sut = CreateSut(fake);

        var result = await sut.TurnOnAsync("Banan");

        var unavailable = Assert.IsType<HaResult.DeviceUnavailable>(result);
        Assert.Equal("Banan", unavailable.Name);
    }

    [Fact]
    public async Task GetStateAsync_UnavailableFixture_IsAvailableFalse()
    {
        var fake = new FakeMcpGateway();
        fake.Returns("GetLiveContext", new McpCallResult(false, UnavailableFixture));
        var sut = CreateSut(fake);

        var state = await sut.GetStateAsync("Banan");

        Assert.False(state.IsAvailable);
        Assert.False(state.IsOn);
    }

    // -----------------------------------------------------------------------
    // #9 — Tool error → HaResult.ToolError(message)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task TurnOnAsync_ToolError_ReturnsToolError()
    {
        var fake = new FakeMcpGateway();
        SetupAvailable(fake);
        fake.Returns("HassTurnOn", new McpCallResult(true, "Entity not found"));
        var sut = CreateSut(fake);

        var result = await sut.TurnOnAsync("Banan");

        var error = Assert.IsType<HaResult.ToolError>(result);
        Assert.Equal("Entity not found", error.Message);
    }

    // -----------------------------------------------------------------------
    // #10 — ListToolsAsync delegates to gateway
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ListToolsAsync_ReturnsMcpGatewayTools()
    {
        var fake = new FakeMcpGateway
        {
            ToolList = [new McpToolInfo("HassTurnOn", "Turns on a device"), new McpToolInfo("GetLiveContext", null)]
        };
        var sut = CreateSut(fake);

        var tools = await sut.ListToolsAsync();

        Assert.Equal(2, tools.Count);
        Assert.Contains(tools, t => t.Name == "HassTurnOn");
        Assert.Contains(tools, t => t.Name == "GetLiveContext");
    }
}
