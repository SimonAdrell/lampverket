namespace Lampverket.Agent;

using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using Lampverket.Core;
using Microsoft.Extensions.Logging;

public sealed class AnthropicClaudeClient : IClaudeClient
{
    private readonly IAnthropicClient _anthropic;
    private readonly TimeProvider _clock;
    private readonly ILogger<AnthropicClaudeClient> _logger;

    public AnthropicClaudeClient(IAnthropicClient anthropic, TimeProvider clock, ILogger<AnthropicClaudeClient> logger)
    {
        _anthropic = anthropic;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Beslut?> BegarBeslutAsync(Arende arende, string deviceContext, CancellationToken ct = default)
    {
        var response = await _anthropic.Messages.Create(new MessageCreateParams
        {
            Model = Model.ClaudeSonnet4_6,
            MaxTokens = 1024,
            System = BoSkenSystemPrompt.Build(),
            Tools = [new Tool
            {
                Name = "lamna_beslut",
                Description = "Utfärda ett formellt beslut på ansökan. Anropa alltid detta verktyg.",
                InputSchema = BeslutsSchema.Build(),
            }],
            ToolChoice = new ToolChoiceTool { Name = "lamna_beslut" },
            Messages = [new MessageParam
            {
                Role = Role.User,
                Content = BuildUserMessage(arende, deviceContext),
            }],
        }, cancellationToken: ct);

        ToolUseBlock? toolUse = null;
        foreach (var block in response.Content)
        {
            if (block.TryPickToolUse(out var tu)) { toolUse = tu; break; }
        }
        if (toolUse is null)
        {
            _logger.LogWarning("Claude returned no tool use block for ärende {Diarienummer}", arende.Diarienummer);
            return null;
        }

        return DeserializeBeslut(toolUse.Input, _clock.GetUtcNow());
    }

    private static string BuildUserMessage(Arende arende, string deviceContext) => $"""
        Diarienummer: {arende.Diarienummer}
        Sökande: {arende.Ansokan.Sokande}
        Ärendetyp: {arende.Ansokan.Arendetyp}
        Berörd enhet: {arende.Ansokan.BerordEnhet}
        Önskad åtgärd: {arende.Ansokan.OnskadAtgard ?? "(ej angiven)"}
        Motivering: {arende.Ansokan.Motivering}
        Önskat datum: {arende.Ansokan.OnskatDatum:yyyy-MM-dd}

        Aktuellt enhetsstatus:
        {deviceContext}
        """;

    private static Beslut? DeserializeBeslut(IReadOnlyDictionary<string, JsonElement> input, DateTimeOffset now)
    {
        try
        {
            if (!input.TryGetValue("beslutstyp", out var beslutstypEl)) return null;
            if (!Enum.TryParse<Beslutstyp>(beslutstypEl.GetString(), true, out var beslutstyp)) return null;

            string[]? lagrum = null;
            if (input.TryGetValue("lagrum", out var lagrumEl) && lagrumEl.ValueKind == JsonValueKind.Array)
                lagrum = [.. lagrumEl.EnumerateArray().Select(e => e.GetString() ?? "")];

            return new Beslut
            {
                Beslutstyp = beslutstyp,
                Beslutstext = input.TryGetValue("beslutstext", out var bt) ? bt.GetString() ?? "" : "",
                Motivering = input.TryGetValue("motivering", out var mot) ? mot.GetString() ?? "" : "",
                Lagrum = lagrum ?? [],
                Overklagandehanvisning = input.TryGetValue("overklagandehanvisning", out var ok) ? ok.GetString() ?? "" : "",
                Verkstallighet = input.TryGetValue("verkstallighet", out var verk) ? verk.GetString() ?? "" : "",
                Datum = now,
            };
        }
        catch (Exception)
        {
            return null;
        }
    }
}
