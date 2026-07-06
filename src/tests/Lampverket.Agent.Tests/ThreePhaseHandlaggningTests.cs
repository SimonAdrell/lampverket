using System.Text.Json;
using Anthropic.Helpers.Beta;
using Anthropic.Models.Beta.Messages;
using Lampverket.Agent.HomeAssistant;
using Lampverket.Core;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lampverket.Agent.Tests;

public class ThreePhaseHandlaggningTests
{
    [Fact]
    public void BeslutsSchema_IsStrictCompatibleAndStreamsMotiveringEarly()
    {
        var schema = BeslutsSchema.Build();

        Assert.Equal("object", schema.Type.GetString());
        Assert.False(schema.RawData["additionalProperties"].GetBoolean());
        Assert.Equal(
            ["beslutstyp", "beslutstext", "motivering", "lagrum", "overklagandehanvisning", "verkstallighet"],
            schema.Required);
        Assert.NotNull(schema.Properties);
        var properties = schema.Properties!;
        Assert.Equal(
            ["beslutstyp", "beslutstext", "motivering"],
            properties.Keys.Take(3));
    }

    [Fact]
    public void TryExtractMotivering_DecodesUnicodeEscapes()
    {
        var motivering = HandlaggareAgent.TryExtractMotivering(
            """{"beslutstyp":"Avslag","motivering":"Prövning enligt f\u00f6rvaltningslagen.\nRad två"}""");

        Assert.Equal("Prövning enligt förvaltningslagen.\nRad två", motivering);
    }

    [Fact]
    public void TryExtractMotivering_WaitsWhenUnicodeEscapeIsIncomplete()
    {
        var partial = HandlaggareAgent.TryExtractMotivering("""{"motivering":"Prövning enligt f\u00""");
        var complete = HandlaggareAgent.TryExtractMotivering("""{"motivering":"Prövning enligt f\u00f6rvaltningslagen"}""");

        Assert.Equal("Prövning enligt f", partial);
        Assert.Equal("Prövning enligt förvaltningslagen", complete);
    }

    [Fact]
    public void BuildUserMessageWithLiveContext_IncludesLiveContextAsPlainText()
    {
        var message = TestArende().BuildUserMessageWithLiveContext("state: off");

        Assert.Contains("Aktuellt hemtillstånd (GetLiveContext):", message, StringComparison.Ordinal);
        Assert.Contains("state: off", message, StringComparison.Ordinal);
        Assert.DoesNotContain("tool_result", message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildUserMessageWithLiveContext_ForwardsErrorPayloadAsDecisionInput()
    {
        // Fas 1 kastar aldrig: en otillgänglig enhet/MCP-fel returneras som text och måste ändå
        // nå beslutsfasen så modellen kan bordlägga/avslå i karaktär.
        var errorPayload = "GetLiveContext returnerade fel för light.banan: entity unavailable";

        var message = TestArende().BuildUserMessageWithLiveContext(errorPayload);

        Assert.Contains("Aktuellt hemtillstånd (GetLiveContext):", message, StringComparison.Ordinal);
        Assert.Contains(errorPayload, message, StringComparison.Ordinal);
    }

    private static Arende TestArende() => new(
        "LV-2026-000001",
        DateTimeOffset.Parse("2026-07-06T12:00:00Z"),
        new Ansokan
        {
            Personnummer = "19900101-1234",
            Sokande = "Test Testsson",
            Arendetyp = Arendetyp.Tanding,
            BerordEnhet = new Enhet("light.banan", "Banan"),
            Motivering = "Det är mörkt.",
            OnskatDatum = new DateOnly(2026, 7, 6),
        },
        Arendestatus.Inkommet);

    [Fact]
    public async Task BuildHaActionTools_ExcludesDecisionAndLiveContextAndStillGuardsActions()
    {
        var factory = new HaToolFactory(NullLogger<HaToolFactory>.Instance);
        var slot = new BeslutSlot();

        var tools = factory.BuildHaActionTools(
            [new FakeTool("GetLiveContext"), new FakeTool("HassTurnOn")],
            slot,
            "LV-2026-000001");

        var action = Assert.Single(tools);
        Assert.Equal("HassTurnOn", action.Name);

        var result = await action.ExecuteAsync(new BetaToolUseBlock
        {
            ID = "toolu_test",
            Name = "HassTurnOn",
            Input = new Dictionary<string, JsonElement>
            {
                ["name"] = JsonSerializer.SerializeToElement("light.banan"),
            },
        }, CancellationToken.None);

        Assert.Contains("lamna_beslut", result.Json.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildHaActionTools_RejectsWrongEntityBeforeExecutingAction()
    {
        var factory = new HaToolFactory(NullLogger<HaToolFactory>.Instance);
        var slot = new BeslutSlot();
        Assert.True(slot.TrySet(new Bifall(
            "Lampverket bifaller ansökan",
            "Prövning visar att åtgärden kan genomföras.",
            ["LVFS 2026:1"],
            "Beslutet kan överklagas.",
            "Tänd Banan.",
            DateTimeOffset.Parse("2026-07-06T12:00:00Z"))));
        var inner = new FakeTool("HassTurnOn");

        var action = Assert.Single(factory.BuildHaActionTools(
            [inner],
            slot,
            "LV-2026-000001",
            allowedEntityId: "light.banan"));

        var result = await action.ExecuteAsync(new BetaToolUseBlock
        {
            ID = "toolu_test",
            Name = "HassTurnOn",
            Input = new Dictionary<string, JsonElement>
            {
                ["name"] = JsonSerializer.SerializeToElement("light.big_light_livingroom"),
            },
        }, CancellationToken.None);

        Assert.Contains("light.banan", result.Json.ToString(), StringComparison.Ordinal);
        Assert.Equal(0, inner.CallCount);
        Assert.Null(slot.Verkstallighetsutfall);
    }

    [Fact]
    public async Task BuildHaActionTools_DoesNotExecuteAdditionalActionsAfterConfirmedSuccess()
    {
        var factory = new HaToolFactory(NullLogger<HaToolFactory>.Instance);
        var slot = new BeslutSlot();
        Assert.True(slot.TrySet(new Bifall(
            "Lampverket bifaller ansökan",
            "Prövning visar att åtgärden kan genomföras.",
            ["LVFS 2026:1"],
            "Beslutet kan överklagas.",
            "Tänd Banan.",
            DateTimeOffset.Parse("2026-07-06T12:00:00Z"))));
        var inner = new FakeTool("HassTurnOn");
        var action = Assert.Single(factory.BuildHaActionTools(
            [inner],
            slot,
            "LV-2026-000001",
            allowedEntityId: "light.banan"));
        var toolUse = new BetaToolUseBlock
        {
            ID = "toolu_test",
            Name = "HassTurnOn",
            Input = new Dictionary<string, JsonElement>
            {
                ["name"] = JsonSerializer.SerializeToElement("light.banan"),
            },
        };

        await action.ExecuteAsync(toolUse, CancellationToken.None);
        var result = await action.ExecuteAsync(toolUse, CancellationToken.None);

        Assert.Equal(1, inner.CallCount);
        Assert.Equal(Verkstallighetsstatus.Verkstalld, slot.Verkstallighetsutfall);
        Assert.Contains("redan", result.Json.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BeslutSlot_DoesNotLetFailureOverwriteConfirmedSuccess()
    {
        var slot = new BeslutSlot();

        slot.RegistreraVerkstallighetsforsok(lyckat: true);
        slot.RegistreraVerkstallighetsforsok(lyckat: false);

        Assert.Equal(Verkstallighetsstatus.Verkstalld, slot.Verkstallighetsutfall);
    }

    private sealed class FakeTool(string name) : IBetaRunnableTool
    {
        private int _callCount;

        public string Name => name;
        public int CallCount => _callCount;

        public BetaToolUnion Definition => new(new BetaTool
        {
            Name = name,
            Description = name,
            InputSchema = new InputSchema(new Dictionary<string, JsonElement>
            {
                ["type"] = JsonSerializer.SerializeToElement("object"),
                ["properties"] = JsonSerializer.SerializeToElement(new Dictionary<string, JsonElement>()),
                ["required"] = JsonSerializer.SerializeToElement(Array.Empty<string>()),
            }),
        });

        public Task<BetaToolResultBlockParamContent> ExecuteAsync(BetaToolUseBlock toolUse, CancellationToken ct)
        {
            _callCount++;
            return Task.FromResult<BetaToolResultBlockParamContent>("ok");
        }
    }
}
