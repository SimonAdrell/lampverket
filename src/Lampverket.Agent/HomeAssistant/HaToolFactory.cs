using Anthropic.Helpers.Beta;
using Anthropic.Models.Beta.Messages;
using Lampverket.Core;
using Microsoft.Extensions.Logging;

namespace Lampverket.Agent.HomeAssistant;

public sealed class HaToolFactory(TimeProvider clock, ILogger<HaToolFactory> logger)
{
    private const string BeslutToolName = "lamna_beslut";
    private static readonly HashSet<string> _beslutExemptTools = ["GetLiveContext"];

    private readonly TimeProvider _clock = clock;
    private readonly ILogger<HaToolFactory> _logger = logger;

    public IReadOnlyList<IBetaRunnableTool> Build(
        IReadOnlyList<IBetaRunnableTool> haTools,
        BeslutSlot slot,
        string diarienummer)
    {
        var tools = new List<IBetaRunnableTool>(haTools.Count + 1);
        foreach (var haTool in haTools)
            tools.Add(GuardHaTool(haTool, slot, diarienummer));
        tools.Add(BuildBeslutTool(slot, diarienummer));
        return tools;
    }

    private BetaRunnableTool GuardHaTool(IBetaRunnableTool inner, BeslutSlot slot, string diarienummer) =>
        new()
        {
            Name = inner.Name,
            Definition = inner.Definition,
            Run = async (toolUse, ct) =>
            {
                var arVerkstallande = !_beslutExemptTools.Contains(toolUse.Name);
                if (arVerkstallande)
                {
                    if (slot.Beslut is not { } beslut)
                    {
                        return NoBeslutError(toolUse.Name, diarienummer);
                    }

                    if (!beslut.TillaterVerkstallighet)
                    {
                        return DecisionForbidsError(toolUse.Name, beslut);
                    }
                }

                return await ExecuteHaToolAsync(inner, toolUse, slot, arVerkstallande, diarienummer, ct);
            },
        };

    private string NoBeslutError(string toolName, string diarienummer)
    {
        _logger.LogWarning(
            "Agent for {Diarienummer} called {ToolName} before lamna_beslut",
            diarienummer, toolName);

        return $"Handläggningsfel: verktyget {toolName} kan inte anropas innan " +
               "ett beslut har utfärdats via lamna_beslut. Utfärda först ett beslut.";
    }

    private static string DecisionForbidsError(string toolName, Beslut beslut)
    {
        return $"Verktyget {toolName} anropades men beslutet ({beslut.GetType().Name}) " +
               "medger ingen verkställighet; ingen åtgärd utförs.";
    }

    private async Task<BetaToolResultBlockParamContent> ExecuteHaToolAsync(
        IBetaRunnableTool inner, BetaToolUseBlock toolUse, BeslutSlot slot, bool arVerkstallande,
        string diarienummer, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation(
                "Agent for {Diarienummer} called {ToolName}", diarienummer, toolUse.Name);

            var toolResponse = await inner.ExecuteAsync(toolUse, ct);

            _logger.LogInformation(
                "Agent for {Diarienummer} responded {ToolName} {Json}", diarienummer, toolUse.Name, toolResponse.Json.ToString());

            // Record the outcome so C# can reconcile the ärende status — a query tool is not an action.
            if (arVerkstallande)
            {
                slot.RegistreraVerkstallighetsforsok(lyckat: true);
            }

            return toolResponse;
        }
        catch (BetaToolError ex)
        {
            // HA returned isError:true — log the actual HA content and re-throw so
            // BetaToolRunner passes it back to Claude as an error tool result. C# only observes.
            if (arVerkstallande)
            {
                slot.RegistreraVerkstallighetsforsok(lyckat: false);
            }
            _logger.LogWarning(
                "MCP call {ToolName} failed for {Diarienummer}: {Content}",
                toolUse.Name, diarienummer, ex.Content.ToString());
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (arVerkstallande)
            {
                slot.RegistreraVerkstallighetsforsok(lyckat: false);
            }
            _logger.LogWarning(ex,
                "MCP call {ToolName} failed for {Diarienummer}: {Message}",
                toolUse.Name, diarienummer, ex.Message);
            return $"Verkställighetshinder: {toolUse.Name} kunde inte utföras " +
                   $"({ex.GetType().Name}: {ex.Message}). Bordlägg ärendet i karaktär.";
        }
    }

    private BetaRunnableTool BuildBeslutTool(BeslutSlot slot, string diarienummer) =>
        new()
        {
            Name = BeslutToolName,
            Definition = new BetaToolUnion(new BetaTool
            {
                Name = BeslutToolName,
                Description = "Utfärda ett formellt beslut på ansökan. MÅSTE anropas före varje verkställande HA-anrop.",
                InputSchema = BeslutsSchema.Build(),
                CacheControl = new BetaCacheControlEphemeral(),
            }),
            Run = (toolUse, _) =>
            {
                var beslut = BeslutParser.TryParse(toolUse.Input, _clock.GetUtcNow(), diarienummer, _logger);
                if (beslut is null)
                {
                    return Task.FromResult<BetaToolResultBlockParamContent>(
                        "Beslutet kunde inte registreras: ogiltig struktur. " +
                        "Anropa lamna_beslut igen med samtliga obligatoriska fält.");
                }

                if (!slot.TrySet(beslut))
                {
                    return Task.FromResult<BetaToolResultBlockParamContent>(
                        "Ett beslut har redan utfärdats för detta ärende. " +
                        "Ytterligare beslut kan inte registreras.");
                }

                return Task.FromResult<BetaToolResultBlockParamContent>(
                    $"Beslutet är registrerat i diariet. Beslutstyp: {beslut.GetType().Name}. " +
                    $"Beslutstext: {beslut.Beslutstext} " +
                    $"Verkställighet enligt beslutet: {beslut.Verkstallighet}");
            },
        };
}
