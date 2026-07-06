using System.Diagnostics;
using Anthropic.Helpers.Beta;
using Anthropic.Models.Beta.Messages;
using Lampverket.Core;
using Microsoft.Extensions.Logging;

namespace Lampverket.Agent.HomeAssistant;

public sealed class HaToolFactory(ILogger<HaToolFactory> logger)
{
    private const string BeslutToolName = "lamna_beslut";
    private static readonly HashSet<string> _beslutExemptTools = ["GetLiveContext"];

    private readonly ILogger<HaToolFactory> _logger = logger;

    public IReadOnlyList<IBetaRunnableTool> BuildHaActionTools(
        IReadOnlyList<IBetaRunnableTool> haTools,
        BeslutSlot slot,
        string diarienummer,
        string? allowedEntityId = null,
        IProgress<Handlaggningshandelse>? progress = null)
    {
        var tools = new List<IBetaRunnableTool>(haTools.Count);
        foreach (var haTool in haTools.Where(t => !_beslutExemptTools.Contains(t.Name)))
        {
            tools.Add(GuardHaTool(haTool, slot, diarienummer, allowedEntityId, progress));
        }

        return tools;
    }

    // Only the definition is consumed: phase 2 forces tool_choice and registers the streamed input
    // via HandlaggareAgent.TryRegisterBeslut, so no runner ever executes this tool.
    public static BetaToolUnion BuildBeslutTool() => new(new BetaTool
    {
        Name = BeslutToolName,
        Description = "Utfärda ett formellt beslut på ansökan. MÅSTE anropas före varje verkställande HA-anrop.",
        InputSchema = BeslutsSchema.Build(),
        EagerInputStreaming = true,
        Strict = true,
        // No cache_control: cached via the system-prompt prefix; no separate breakpoint needed.
    });

    private BetaRunnableTool GuardHaTool(
        IBetaRunnableTool inner, BeslutSlot slot, string diarienummer,
        string? allowedEntityId,
        IProgress<Handlaggningshandelse>? progress) =>
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

                    if (slot.Verkstallighetsutfall == Verkstallighetsstatus.Verkstalld)
                    {
                        return $"Verkställighet är redan bekräftad för {diarienummer}. " +
                               $"Ytterligare anrop till {toolUse.Name} utförs inte.";
                    }

                    if (!IsAllowedEntity(toolUse, allowedEntityId))
                    {
                        return WrongEntityError(toolUse.Name, diarienummer, allowedEntityId, TryGetToolNameArgument(toolUse));
                    }

                    // Beslutet är fattat och medger verkställighet; åtgärden är på väg att utföras.
                    Activity.Current?.AddEvent(new ActivityEvent("first HA action",
                        tags: new ActivityTagsCollection { { "tool.name", toolUse.Name } }));
                    progress?.Report(Handlaggningshandelse.ForSteg("Verkställer"));
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

    private string WrongEntityError(
        string toolName,
        string diarienummer,
        string? allowedEntityId,
        string? requestedName)
    {
        _logger.LogWarning(
            "Agent for {Diarienummer} called {ToolName} for wrong entity {RequestedEntity}; allowed entity is {AllowedEntity}",
            diarienummer, toolName, requestedName, allowedEntityId);

        return $"Verktyget {toolName} avvisades: ärendet gäller endast entity-id " +
               $"{allowedEntityId}. Anropa verkställande HA-funktion med exakt detta värde som `name`.";
    }

    private static bool IsAllowedEntity(BetaToolUseBlock toolUse, string? allowedEntityId)
    {
        if (string.IsNullOrWhiteSpace(allowedEntityId))
        {
            return true;
        }

        return string.Equals(TryGetToolNameArgument(toolUse), allowedEntityId, StringComparison.Ordinal);
    }

    private static string? TryGetToolNameArgument(BetaToolUseBlock toolUse)
    {
        if (toolUse.Input.TryGetValue("name", out var name) && name.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            return name.GetString();
        }

        return null;
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
}
