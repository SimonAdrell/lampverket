using Bunit;
using Lampverket.Core;
using Lampverket.Web.Components.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace Lampverket.Web.Tests.Pages;

public class ArendeTests
{
    private static BunitContext CreateCtx(IAnsokanService service)
    {
        var ctx = new BunitContext();
        ctx.Services.AddSingleton(service);
        ctx.Services.AddSingleton<IArendeNotifier, ArendeNotifier>();
        return ctx;
    }

    private static Core.Ansokan FakeAnsokan() => new()
    {
        Personnummer = "19900101-1234",
        Sokande = "Testaren",
        Arendetyp = Arendetyp.Tanding,
        BerordEnhet = new Enhet("light.test", "Testlampa"),
        Motivering = "Det är mörkt.",
        OnskatDatum = DateOnly.FromDateTime(DateTime.Today)
    };

    // ── backlog #13: initial state ────────────────────────────────────────────
    [Fact]
    public void Arende_NoDecision_Shows_UnderHandlaggning()
    {
        var pending = new FakeArendeService(new Core.Arende(
            Diarienummer: "LV-2026-000001",
            Mottaget: DateTimeOffset.UtcNow,
            Ansokan: FakeAnsokan(),
            Status: Arendestatus.Inkommet
        ));
        using var ctx = CreateCtx(pending);
        var cut = ctx.Render<ArendeDetalj>(p =>
            p.Add(x => x.Diarienummer, "LV-2026-000001"));
        Assert.Contains("handläggning", cut.Markup.ToLowerInvariant());
    }

    [Fact]
    public void Arende_NoDecision_Shows_Diarienummer()
    {
        var pending = new FakeArendeService(new Core.Arende(
            Diarienummer: "LV-2026-000099",
            Mottaget: DateTimeOffset.UtcNow,
            Ansokan: FakeAnsokan(),
            Status: Arendestatus.Inkommet
        ));
        using var ctx = CreateCtx(pending);
        var cut = ctx.Render<ArendeDetalj>(p =>
            p.Add(x => x.Diarienummer, "LV-2026-000099"));
        Assert.Contains("LV-2026-000099", cut.Markup);
    }

    // ── backlog #14: beslut card renders ─────────────────────────────────────
    [Fact]
    public void Arende_WithBeslut_RendersBeslutCard()
    {
        var decided = new FakeArendeService(new Core.Arende(
            Diarienummer: "LV-2026-000001",
            Mottaget: DateTimeOffset.UtcNow,
            Ansokan: FakeAnsokan(),
            Status: Arendestatus.Verkstallt,
            Beslut: new Core.Bifall(
                Beslutstext: "Lampverket beviljar ansökan.",
                Motivering: "Berättigat behov.",
                Lagrum: ["7 § lagen (2026:1)"],
                Overklagandehanvisning: "Kan överklagas inom 3 veckor.",
                VerkstallighetsText: "Belysningen tändes kl. 21:42.",
                Datum: DateTimeOffset.UtcNow)
        ));
        using var ctx = CreateCtx(decided);
        var cut = ctx.Render<ArendeDetalj>(p =>
            p.Add(x => x.Diarienummer, "LV-2026-000001"));
        Assert.NotNull(cut.Find(".beslut-card"));
        Assert.Contains("Lampverket beviljar", cut.Markup);
    }

    [Fact]
    public void Arende_WithBeslut_ShowsBeslutstypLabel()
    {
        var decided = new FakeArendeService(new Core.Arende(
            Diarienummer: "LV-2026-000001",
            Mottaget: DateTimeOffset.UtcNow,
            Ansokan: FakeAnsokan(),
            Status: Arendestatus.Verkstallt,
            Beslut: new Core.Bifall(
                Beslutstext: "Lampverket beviljar ansökan.",
                Motivering: "OK.",
                Lagrum: [],
                Overklagandehanvisning: "Kan överklagas.",
                VerkstallighetsText: "Utfört.",
                Datum: DateTimeOffset.UtcNow)
        ));
        using var ctx = CreateCtx(decided);
        var cut = ctx.Render<ArendeDetalj>(p =>
            p.Add(x => x.Diarienummer, "LV-2026-000001"));
        Assert.Contains("Bifall", cut.Markup);
    }

    [Fact]
    public void Arende_BifallButExecutionFailed_ShowsHinderNotFact()
    {
        var decided = new FakeArendeService(new Core.Arende(
            Diarienummer: "LV-2026-000001",
            Mottaget: DateTimeOffset.UtcNow,
            Ansokan: FakeAnsokan(),
            Status: Arendestatus.Beslutat,
            Beslut: new Core.Bifall(
                Beslutstext: "Lampverket beviljar ansökan.",
                Motivering: "OK.",
                Lagrum: [],
                Overklagandehanvisning: "Kan överklagas.",
                VerkstallighetsText: "Lampan tänds.",
                Datum: DateTimeOffset.UtcNow),
            Verkstallighetsutfall: Verkstallighetsstatus.Misslyckad
        ));
        using var ctx = CreateCtx(decided);
        var cut = ctx.Render<ArendeDetalj>(p =>
            p.Add(x => x.Diarienummer, "LV-2026-000001"));

        // Åtgärden presenteras inte som utförd; hindret ska synas och statusen vara Beslutat.
        Assert.Contains("Verkställighetshinder", cut.Markup);
        Assert.Contains("Beslutad åtgärd:", cut.Markup);
        Assert.Contains("status-pill beslutat", cut.Markup);
    }

    [Fact]
    public void Arende_BifallUtanAtgard_ShowsEjBekraftatNotFact()
    {
        // Bifall där inget verkställande anrop gjordes: status Beslutat, utfall null.
        // Åtgärden får inte presenteras som utförd; visa "ännu inte bekräftats", inte hinder-framing.
        var decided = new FakeArendeService(new Core.Arende(
            Diarienummer: "LV-2026-000001",
            Mottaget: DateTimeOffset.UtcNow,
            Ansokan: FakeAnsokan(),
            Status: Arendestatus.Beslutat,
            Beslut: new Core.Bifall(
                Beslutstext: "Lampverket beviljar ansökan.",
                Motivering: "OK.",
                Lagrum: [],
                Overklagandehanvisning: "Kan överklagas.",
                VerkstallighetsText: "Lampan tänds.",
                Datum: DateTimeOffset.UtcNow),
            Verkstallighetsutfall: null
        ));
        using var ctx = CreateCtx(decided);
        var cut = ctx.Render<ArendeDetalj>(p =>
            p.Add(x => x.Diarienummer, "LV-2026-000001"));

        // Ärlig framställning: beslutad åtgärd, ännu ej bekräftad — inte "hinder" (inget försök misslyckades).
        Assert.Contains("Beslutad åtgärd:", cut.Markup);
        Assert.Contains("ännu inte bekräftats", cut.Markup);
        Assert.DoesNotContain("Verkställighetshinder", cut.Markup);
        Assert.Contains("status-pill beslutat", cut.Markup);
    }

    [Fact]
    public void Arende_LegacyRowNullUtfall_RendersLikeVerkstallt()
    {
        // Gamla diariet-rader saknar verkställighetsutfall (null) — ska renderas som förr.
        var decided = new FakeArendeService(new Core.Arende(
            Diarienummer: "LV-2026-000001",
            Mottaget: DateTimeOffset.UtcNow,
            Ansokan: FakeAnsokan(),
            Status: Arendestatus.Verkstallt,
            Beslut: new Core.Bifall(
                Beslutstext: "Lampverket beviljar ansökan.",
                Motivering: "OK.",
                Lagrum: [],
                Overklagandehanvisning: "Kan överklagas.",
                VerkstallighetsText: "Lampan tändes.",
                Datum: DateTimeOffset.UtcNow)
        ));
        using var ctx = CreateCtx(decided);
        var cut = ctx.Render<ArendeDetalj>(p =>
            p.Add(x => x.Diarienummer, "LV-2026-000001"));

        Assert.DoesNotContain("Verkställighetshinder", cut.Markup);
        Assert.Contains("Lampan tändes.", cut.Markup);
    }
}

file sealed class FakeArendeService(Core.Arende arende) : IAnsokanService
{
    private readonly Core.Arende _arende = arende;

    public Task<Core.Arende> RegisterAnsokanAsync(Core.Ansokan ansokan) =>
        throw new NotImplementedException();

    public Task<Core.Arende?> HamtaArendeAsync(string diarienummer) =>
        Task.FromResult<Core.Arende?>(diarienummer == _arende.Diarienummer ? _arende : null);
}
