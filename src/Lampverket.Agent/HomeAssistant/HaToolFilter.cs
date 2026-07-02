using Anthropic.Helpers.Beta;

namespace Lampverket.Agent.HomeAssistant;

public static class HaToolFilter
{
    private static readonly HashSet<string> _light =
        ["GetLiveContext", "HassTurnOn", "HassTurnOff", "HassLightSet"];

    private static readonly HashSet<string> _mediaPlayer =
        ["GetLiveContext", "HassTurnOn", "HassTurnOff", "HassSetVolume", "HassMediaSearchAndPlay"];

    private static readonly HashSet<string> _default =
        ["GetLiveContext", "HassTurnOn", "HassTurnOff"];

    public static IReadOnlyList<IBetaRunnableTool> ForEntity(
        IReadOnlyList<IBetaRunnableTool> tools, string entityId)
    {
        var allowed = entityId.Split('.')[0] switch
        {
            "light" => _light,
            "media_player" => _mediaPlayer,
            _ => _default,
        };
        return tools.Where(t => allowed.Contains(t.Name)).ToList();
    }
}
