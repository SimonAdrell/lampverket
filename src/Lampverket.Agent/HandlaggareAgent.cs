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

        var parameters = new MessageCreateParams
        {
            Model = AnthropicModel.ClaudeSonnet4_6,
            MaxTokens = 4096,
            System = _systemPrompt,
            Messages =
            [
                new BetaMessageParam
                {
                    Role = Role.User,
                    Content = arende.BuildUserMessage(),
                },
            ],
        };

        var toolRunner = _anthropic.Beta.Messages.ToolRunner(parameters, tools, maxIterations: MaxIterations);

        var iterations = 0;
        await foreach (var message in toolRunner.WithCancellation(ct))
        {
            iterations++;
            _logger.LogDebug(
                "Agent turn {Iteration} for {Diarienummer}: stop_reason={StopReason}",
                iterations, arende.Diarienummer, message.StopReason);

            if (message.StopReason != BetaStopReason.ToolUse)
            {
                _logger.LogInformation(
                    "Agent loop for {Diarienummer} ended after {Iterations} iteration(s); stop_reason={StopReason}", arende.Diarienummer, iterations, message.StopReason);
            }
        }

        if (slot.Beslut is null)
        {
            _logger.LogWarning(
                "Agent loop for {Diarienummer} ended without a valid beslut after {Iterations} iteration(s)",
                arende.Diarienummer, iterations);
            return new Handlaggningsresultat(Beslut.BordlaggUtanBeslut(_clock.GetUtcNow()), null);
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

}
