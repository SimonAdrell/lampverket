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
}
