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
    private const int MaxIterations = 10;

    // Verkställighetsnudgen: en avgränsad andra runda. Lyckoscenariot är GetLiveContext →
    // verkställande anrop → kort bekräftelse; 4 ger marginal utan att släppa loss loopen.
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

    public async Task<Handlaggningsresultat> HandlaggaAsync(Arende arende, CancellationToken ct = default)
    {
        var haTools = await _mcpProvider.GetToolsAsync(ct);
        var device = _haOptions.Devices
            .FirstOrDefault(d => d.EntityId == arende.Ansokan.BerordEnhet.EntityId);

        if (device is null)
        {
            return new Handlaggningsresultat(
                Beslut.BordlaggOkandEnhet(_clock.GetUtcNow(), arende.Ansokan.BerordEnhet.FriendlyName), null);
        }

        var filtered = HaToolFilter.ForEntity(haTools, device.EntityId);
        var slot = new BeslutSlot();
        var tools = _toolFactory.Build(filtered, slot, arende.Diarienummer);

        await DriveToolLoopAsync(
            BuildParameters(arende.BuildUserMessage()), tools, MaxIterations, arende.Diarienummer, ct);

        if (slot.Beslut is null)
        {
            _logger.LogWarning(
                "Agent loop for {Diarienummer} ended without a valid beslut",
                arende.Diarienummer);
            return new Handlaggningsresultat(Beslut.BordlaggUtanBeslut(_clock.GetUtcNow()), null);
        }

        // Åtgärd efter bifall: ett bifall som aldrig verkställdes nudgas att verkställas i en
        // andra, avgränsad runda. Beslutet ligger redan i slot, så guarden släpper igenom det
        // verkställande anropet — åtgärden reser fortfarande genom Claudes tool_use (konvention #8).
        if (BehoverVerkstallighetsnudge(slot.Beslut, slot.Verkstallighetsutfall)
            && slot.Beslut is VerkstalltBeslut nudgeBeslut)
        {
            await NudgaVerkstallighetAsync(arende, device.EntityId, nudgeBeslut, tools, ct);

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

    // Kör en avgränsad andra runda som förelägger Claude att verkställa det redan fattade beslutet.
    // Bästa möjliga insats: ett fel i denna runda får inte bubbla upp och förvandla ett giltigt
    // bifall till en bordläggning (HandlaggareService fångar undantag och bordlägger).
    private async Task NudgaVerkstallighetAsync(
        Arende arende, string entityId, VerkstalltBeslut beslut,
        IReadOnlyList<IBetaRunnableTool> tools, CancellationToken ct)
    {
        _logger.LogWarning(
            "Bifall för {Diarienummer} saknar verkställighet; kör verkställighetsnudge.",
            arende.Diarienummer);
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
    };

    private async Task DriveToolLoopAsync(
        MessageCreateParams parameters, IReadOnlyList<IBetaRunnableTool> tools,
        int maxIterations, string diarienummer, CancellationToken ct)
    {
        var toolRunner = _anthropic.Beta.Messages.ToolRunner(parameters, tools, maxIterations: maxIterations);

        var iterations = 0;
        await foreach (var message in toolRunner.WithCancellation(ct))
        {
            iterations++;
            _logger.LogDebug(
                "Agent turn {Iteration} for {Diarienummer}: stop_reason={StopReason}",
                iterations, diarienummer, message.StopReason);

            if (message.StopReason != BetaStopReason.ToolUse)
            {
                _logger.LogInformation(
                    "Agent loop for {Diarienummer} ended after {Iterations} iteration(s); stop_reason={StopReason}",
                    diarienummer, iterations, message.StopReason);
            }
        }
    }
}
