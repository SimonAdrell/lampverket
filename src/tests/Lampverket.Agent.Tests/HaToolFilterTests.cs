using Anthropic.Helpers.Beta;
using Anthropic.Models.Beta.Messages;
using Lampverket.Agent.HomeAssistant;

namespace Lampverket.Agent.Tests;

public class HaToolFilterTests
{
    private static readonly IReadOnlyList<IBetaRunnableTool> AllTools =
    [
        MakeTool("GetLiveContext"),
        MakeTool("HassTurnOn"),
        MakeTool("HassTurnOff"),
        MakeTool("HassLightSet"),
        MakeTool("HassSetVolume"),
        MakeTool("HassMediaSearchAndPlay"),
    ];

    [Fact]
    public void ForEntity_LightDomain_IncludesLightSetExcludesMediaPlayerTools()
    {
        var result = HaToolFilter.ForEntity(AllTools, "light.vardagsrum");

        Assert.Equal(
            ["GetLiveContext", "HassLightSet", "HassTurnOff", "HassTurnOn"],
            result.Select(t => t.Name).Order());
    }

    [Fact]
    public void ForEntity_MediaPlayerDomain_IncludesMediaToolsExcludesLightSet()
    {
        var result = HaToolFilter.ForEntity(AllTools, "media_player.spotify");

        Assert.Equal(
            ["GetLiveContext", "HassMediaSearchAndPlay", "HassSetVolume", "HassTurnOff", "HassTurnOn"],
            result.Select(t => t.Name).Order());
    }

    [Fact]
    public void ForEntity_UnknownDomain_ReturnsOnlyBasicTools()
    {
        var result = HaToolFilter.ForEntity(AllTools, "switch.badrum");

        Assert.Equal(
            ["GetLiveContext", "HassTurnOff", "HassTurnOn"],
            result.Select(t => t.Name).Order());
    }

    [Fact]
    public void ForEntity_EmptyEntityId_ReturnsOnlyBasicTools()
    {
        var result = HaToolFilter.ForEntity(AllTools, "");

        Assert.Equal(
            ["GetLiveContext", "HassTurnOff", "HassTurnOn"],
            result.Select(t => t.Name).Order());
    }

    [Fact]
    public void ForEntity_ToolNotInAllowedSet_IsExcluded()
    {
        var result = HaToolFilter.ForEntity(AllTools, "light.kokslampan");

        Assert.DoesNotContain(result, t => t.Name == "HassSetVolume");
        Assert.DoesNotContain(result, t => t.Name == "HassMediaSearchAndPlay");
    }

    private static IBetaRunnableTool MakeTool(string name) => new FakeTool(name);
}

file sealed class FakeTool(string name) : IBetaRunnableTool
{
    public string Name => name;
    public BetaToolUnion Definition => throw new NotSupportedException();
    public Task<BetaToolResultBlockParamContent> ExecuteAsync(BetaToolUseBlock toolUse, CancellationToken ct)
        => throw new NotSupportedException();
}
