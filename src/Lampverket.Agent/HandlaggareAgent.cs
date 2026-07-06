using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Helpers.Beta;
using Anthropic.Models.Beta.Messages;
using Lampverket.Agent.HomeAssistant;
using Lampverket.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AnthropicModel = Anthropic.Models.Messages.Model;

namespace Lampverket.Agent;

/// <summary>
/// Multi-turn agentic loop. Drives Claude via <see cref="BetaToolRunner"/> against the Home
/// Assistant MCP server (tools loaded by <see cref="HaMcpProvider"/>) and a custom
/// <c>lamna_beslut</c> tool built by <see cref="HaToolFactory"/>.
/// </summary>
public sealed class HandlaggareAgent : IHandlaggareAgent
{
    private const int DecisionMaxAttempts = 2;
    private const string BeslutToolName = "lamna_beslut";

    // Verkställighetsnudgen: en avgränsad andra runda. Kontexten hämtades i fas 1 och kan vara
    // upp till några sekunder gammal här; fas 3 får därför bara verkställande verktyg.
    private const int VerkstallighetsnudgeMaxIterations = 4;

    private readonly IAnthropicClient _anthropic;
    private readonly HaMcpProvider _mcpProvider;
    private readonly HaToolFactory _toolFactory;
    private readonly HomeAssistantOptions _haOptions;
    private readonly ILogger<HandlaggareAgent> _logger;
    private readonly string _systemPrompt;
    private readonly TimeProvider _clock;

    public HandlaggareAgent(
        IAnthropicClient anthropic,
        HaMcpProvider mcpProvider,
        HaToolFactory toolFactory,
        IOptions<HomeAssistantOptions> haOptions,
        ILogger<HandlaggareAgent> logger,
        TimeProvider clock)
    {
        _anthropic = anthropic;
        _mcpProvider = mcpProvider;
        _toolFactory = toolFactory;
        _haOptions = haOptions.Value;
        _logger = logger;
        _systemPrompt = BoSkenSystemPrompt.Build(_haOptions.Devices);
        _clock = clock;
    }

    public async Task<Handlaggningsresultat> HandlaggaAsync(
        Arende arende, IProgress<Handlaggningshandelse>? progress = null, CancellationToken ct = default)
    {
        var device = _haOptions.Devices
            .FirstOrDefault(d => d.EntityId == arende.Ansokan.BerordEnhet.EntityId);

        if (device is null)
        {
            return new Handlaggningsresultat(
                Beslut.BordlaggOkandEnhet(_clock.GetUtcNow(), arende.Ansokan.BerordEnhet.FriendlyName), null);
        }

        var haTools = await _mcpProvider.GetToolsAsync(ct);
        var filtered = HaToolFilter.ForEntity(haTools, device.EntityId);
        var slot = new BeslutSlot();
        var beslutTool = HaToolFactory.BuildBeslutTool();
        var actionTools = _toolFactory.BuildHaActionTools(filtered, slot, arende.Diarienummer, device.EntityId, progress);

        progress?.Report(Handlaggningshandelse.ForSteg("Granskar hemförhållanden"));
        var liveContext = await FetchLiveContextAsync(device.EntityId, ct);
        await RunDecisionPhaseAsync(arende, liveContext, beslutTool, slot, progress, ct);

        if (slot.Beslut is null)
        {
            _logger.LogWarning(
                "Decision phase for {Diarienummer} ended without a valid beslut",
                arende.Diarienummer);
            return new Handlaggningsresultat(Beslut.BordlaggUtanBeslut(_clock.GetUtcNow()), null);
        }

        // Fas 2 exponerar inga HA-verktyg. Verkställbara beslut kommer därför hit utan utfall och
        // fas 3 blir normalvägen för faktisk Home Assistant-verkställighet.
        if (BehoverVerkstallighetsnudge(slot.Beslut, slot.Verkstallighetsutfall)
            && slot.Beslut is VerkstalltBeslut nudgeBeslut)
        {
            await NudgaVerkstallighetAsync(arende, device.EntityId, nudgeBeslut, actionTools, ct);

            if (slot.Verkstallighetsutfall is null)
            {
                _logger.LogWarning(
                    "Verkställighetsnudge för {Diarienummer} ledde inte till någon åtgärd; ärendet stannar i Beslutat.",
                    arende.Diarienummer);
            }
        }

        var utfall = slot.Verkstallighetsutfall;
        if (slot.Beslut is VerkstalltBeslut && utfall == Verkstallighetsstatus.Misslyckad)
        {
            _logger.LogWarning(
                "Verkställighet misslyckades för {Diarienummer}; ärendet stäms av till Beslutat.",
                arende.Diarienummer);
        }

        return new Handlaggningsresultat(slot.Beslut, utfall);
    }

    /// <summary>
    /// Ett bifall/delvis bifall som medger verkställighet men där inget verkställande anrop
    /// registrerades (utfall == null). Ett misslyckat försök (Misslyckad) nudgas inte om — Claude
    /// har redan sett felet.
    /// </summary>
    public static bool BehoverVerkstallighetsnudge(Beslut? beslut, Verkstallighetsstatus? utfall) =>
        beslut is VerkstalltBeslut && utfall is null;

    private async Task<string> FetchLiveContextAsync(string entityId, CancellationToken ct)
    {
        using var activity = AgentDiagnostics.Source.StartActivity("handläggning.context");
        activity?.SetTag("entity.id", entityId);
        var context = await _mcpProvider.GetLiveContextAsync(entityId, ct);
        activity?.SetTag("context.length", context.Length);
        return context;
    }

    private async Task RunDecisionPhaseAsync(
        Arende arende,
        string liveContext,
        BetaToolUnion beslutTool,
        BeslutSlot slot,
        IProgress<Handlaggningshandelse>? progress,
        CancellationToken ct)
    {
        for (var attempt = 1; attempt <= DecisionMaxAttempts && slot.Beslut is null; attempt++)
        {
            using var activity = AgentDiagnostics.Source.StartActivity("handläggning.decision");
            activity?.SetTag("diarienummer", arende.Diarienummer);
            activity?.SetTag("attempt", attempt);

            var inputJson = await StreamForcedBeslutAsync(
                BuildDecisionParameters(arende, liveContext, beslutTool),
                arende.Diarienummer,
                progress,
                activity,
                ct);

            if (TryRegisterBeslut(inputJson, slot, arende.Diarienummer, progress))
            {
                activity?.SetStatus(ActivityStatusCode.Ok);
                activity?.AddEvent(new ActivityEvent("beslut completed"));
                return;
            }

            activity?.SetStatus(ActivityStatusCode.Error, "invalid beslut");
            if (attempt < DecisionMaxAttempts)
            {
                _logger.LogWarning(
                    "Forced decision attempt {Attempt} for {Diarienummer} produced invalid beslut; retrying once.",
                    attempt, arende.Diarienummer);
            }
        }
    }

    private MessageCreateParams BuildDecisionParameters(
        Arende arende, string liveContext, BetaToolUnion beslutTool) => new()
    {
        Model = AnthropicModel.ClaudeSonnet4_6,
        MaxTokens = 4096,
        System = new List<BetaTextBlockParam>
        {
            new() { Text = _systemPrompt, CacheControl = new BetaCacheControlEphemeral() },
        },
        Messages =
        [
            new BetaMessageParam
            {
                Role = Role.User,
                Content = arende.BuildUserMessageWithLiveContext(liveContext),
            },
        ],
        Tools = [beslutTool],
        ToolChoice = new BetaToolChoice(
            new BetaToolChoiceTool(BeslutToolName) { DisableParallelToolUse = true },
            null),
    };

    private async Task<string> StreamForcedBeslutAsync(
        MessageCreateParams parameters,
        string diarienummer,
        IProgress<Handlaggningshandelse>? progress,
        Activity? activity,
        CancellationToken ct)
    {
        long? beslutToolBlock = null;
        var beslutInputJson = new StringBuilder();
        string? senasteMotivering = null;
        var firstMotiveringReported = false;
        BetaStopReason? stopReason = null;
        var stopwatch = Stopwatch.StartNew();

        await foreach (var streamEvent in _anthropic.Beta.Messages.CreateStreaming(parameters, ct).WithCancellation(ct))
        {
            if (streamEvent.TryPickContentBlockStart(out var start)
                && start.ContentBlock.TryPickBetaToolUse(out var toolUse)
                && toolUse.Name == BeslutToolName)
            {
                beslutToolBlock = start.Index;
                beslutInputJson.Clear();
                activity?.AddEvent(new ActivityEvent("lamna_beslut start"));
                activity?.SetTag("decision.beslut_start_ms", stopwatch.ElapsedMilliseconds);
                progress?.Report(Handlaggningshandelse.ForSteg("Beslut formuleras"));
            }
            else if (streamEvent.TryPickContentBlockDelta(out var delta)
                     && delta.Index == beslutToolBlock
                     && delta.Delta.TryPickInputJson(out var inputJson))
            {
                beslutInputJson.Append(inputJson.PartialJson);
                var motivering = TryExtractMotivering(beslutInputJson.ToString());
                if (!string.IsNullOrWhiteSpace(motivering) && motivering != senasteMotivering)
                {
                    if (!firstMotiveringReported)
                    {
                        activity?.AddEvent(new ActivityEvent("first motivering delta"));
                        activity?.SetTag("decision.first_motivering_ms", stopwatch.ElapsedMilliseconds);
                        firstMotiveringReported = true;
                    }

                    senasteMotivering = motivering;
                    progress?.Report(Handlaggningshandelse.ForMotiveringUtkast(motivering));
                }
            }
            else if (streamEvent.TryPickContentBlockStop(out var stop))
            {
                if (stop.Index == beslutToolBlock)
                {
                    beslutToolBlock = null;
                }
            }
            else if (streamEvent.TryPickDelta(out var messageDelta)
                     && messageDelta.Delta.StopReason is { } messageStopReason)
            {
                stopReason = messageStopReason.Value();
            }
        }

        _logger.LogDebug(
            "Decision phase for {Diarienummer}: stop_reason={StopReason}, input_json_chars={Length}",
            diarienummer, stopReason, beslutInputJson.Length);

        return beslutInputJson.ToString();
    }

    private bool TryRegisterBeslut(
        string inputJson,
        BeslutSlot slot,
        string diarienummer,
        IProgress<Handlaggningshandelse>? progress)
    {
        if (string.IsNullOrWhiteSpace(inputJson))
        {
            return false;
        }

        try
        {
            var input = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(inputJson);
            if (input is null)
            {
                return false;
            }

            var beslut = BeslutParser.TryParse(input, _clock.GetUtcNow(), diarienummer, _logger);
            if (beslut is null || !slot.TrySet(beslut))
            {
                return false;
            }

            progress?.Report(Handlaggningshandelse.ForBeslut(beslut));
            return true;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "Failed to parse streamed beslut JSON for {Diarienummer}: {InputJson}",
                diarienummer, inputJson);
            return false;
        }
    }

    // Kör en avgränsad andra runda som förelägger Claude att verkställa det redan fattade beslutet.
    // Bästa möjliga insats: ett fel i denna runda får inte bubbla upp och förvandla ett giltigt
    // bifall till en bordläggning (HandlaggareService fångar undantag och bordlägger).
    private async Task NudgaVerkstallighetAsync(
        Arende arende, string entityId, VerkstalltBeslut beslut,
        IReadOnlyList<IBetaRunnableTool> tools, CancellationToken ct)
    {
        using var activity = AgentDiagnostics.Source.StartActivity("handläggning.execution");
        activity?.SetTag("diarienummer", arende.Diarienummer);
        activity?.SetTag("entity.id", entityId);

        _logger.LogInformation(
            "Bifall för {Diarienummer} saknar verkställighet; kör verkställighetsnudge med {ToolCount} verktyg: {Tools}.",
            arende.Diarienummer,
            tools.Count,
            string.Join(", ", tools.Select(t => t.Name)));
        try
        {
            await DriveToolLoopAsync(
                BuildParameters(arende.BuildVerkstallighetsnudge(entityId, beslut.Verkstallighet)),
                tools, VerkstallighetsnudgeMaxIterations, arende.Diarienummer, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "Verkställighetsnudge för {Diarienummer} kunde inte genomföras; behåller beslutet oförändrat.",
                arende.Diarienummer);
        }
    }

    // Fas 3 (verkställighetsnudgen) tvingar alltid fram ett verktygsanrop.
    private MessageCreateParams BuildParameters(string userMessage) => new()
    {
        Model = AnthropicModel.ClaudeSonnet4_6,
        MaxTokens = 4096,
        // cache_control is a prefix marker (tools → system → messages); marking the system
        // prompt caches the tool block too, so later loop turns read the prefix from cache.
        System = new List<BetaTextBlockParam>
        {
            new() { Text = _systemPrompt, CacheControl = new BetaCacheControlEphemeral() },
        },
        Messages =
        [
            new BetaMessageParam { Role = Role.User, Content = userMessage },
        ],
        ToolChoice = new BetaToolChoice(new BetaToolChoiceAny { DisableParallelToolUse = true }, null),
    };

    // Fas 3: nudgen exponerar bara verkställande HA-verktyg (inget lamna_beslut), så här behöver
    // loopen bara drivas och stop_reason avläsas för loggen — ingen ström-parsning av beslut.
    private async Task DriveToolLoopAsync(
        MessageCreateParams parameters, IReadOnlyList<IBetaRunnableTool> tools,
        int maxIterations, string diarienummer, CancellationToken ct)
    {
        var toolRunner = _anthropic.Beta.Messages.ToolRunner(parameters, tools, maxIterations: maxIterations);

        var iterations = 0;
        await foreach (var stream in toolRunner.Streaming(ct).WithCancellation(ct))
        {
            iterations++;
            BetaStopReason? stopReason = null;

            await foreach (var streamEvent in stream.WithCancellation(ct))
            {
                if (streamEvent.TryPickDelta(out var messageDelta)
                    && messageDelta.Delta.StopReason is { } messageStopReason)
                {
                    stopReason = messageStopReason.Value();
                }
            }

            _logger.LogDebug(
                "Agent turn {Iteration} for {Diarienummer}: stop_reason={StopReason}",
                iterations, diarienummer, stopReason);

            if (stopReason != BetaStopReason.ToolUse)
            {
                _logger.LogInformation(
                    "Agent loop for {Diarienummer} ended after {Iterations} iteration(s); stop_reason={StopReason}",
                    diarienummer, iterations, stopReason);
            }
        }
    }

    internal static string? TryExtractMotivering(string partialJson)
    {
        const string key = "\"motivering\"";
        var keyIndex = partialJson.IndexOf(key, StringComparison.Ordinal);
        if (keyIndex < 0)
        {
            return null;
        }

        var colon = partialJson.IndexOf(':', keyIndex + key.Length);
        if (colon < 0)
        {
            return null;
        }

        var start = colon + 1;
        while (start < partialJson.Length && char.IsWhiteSpace(partialJson[start]))
        {
            start++;
        }

        if (start >= partialJson.Length || partialJson[start] != '"')
        {
            return null;
        }

        var valueStart = start + 1;
        var valueEnd = valueStart;
        var escaped = false;
        while (valueEnd < partialJson.Length)
        {
            var current = partialJson[valueEnd];
            if (escaped)
            {
                escaped = false;
            }
            else if (current == '\\')
            {
                escaped = true;
            }
            else if (current == '"')
            {
                break;
            }

            valueEnd++;
        }

        var raw = partialJson[valueStart..valueEnd];
        if (raw.Length == 0)
        {
            return null;
        }

        return UnescapePartialJsonString(raw);
    }

    private static string UnescapePartialJsonString(string raw)
    {
        var output = new StringBuilder(raw.Length);
        for (var i = 0; i < raw.Length; i++)
        {
            var current = raw[i];
            if (current != '\\')
            {
                output.Append(current);
                continue;
            }

            if (i + 1 >= raw.Length)
            {
                break;
            }

            var escaped = raw[++i];
            switch (escaped)
            {
                case 'n':
                    output.Append('\n');
                    break;
                case 'r':
                    output.Append('\r');
                    break;
                case 't':
                    output.Append('\t');
                    break;
                case '"':
                    output.Append('"');
                    break;
                case '\\':
                    output.Append('\\');
                    break;
                case 'u':
                    if (i + 4 >= raw.Length)
                    {
                        i = raw.Length;
                        break;
                    }

                    var hex = raw.Substring(i + 1, 4);
                    if (ushort.TryParse(
                            hex,
                            System.Globalization.NumberStyles.HexNumber,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out var codePoint))
                    {
                        output.Append((char)codePoint);
                        i += 4;
                    }
                    else
                    {
                        output.Append("\\u").Append(hex);
                        i += 4;
                    }
                    break;
                default:
                    output.Append(escaped);
                    break;
            }
        }

        return output.ToString();
    }
}
