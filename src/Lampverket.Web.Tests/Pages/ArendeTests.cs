using Bunit;
using Lampverket.Core;
using Lampverket.Web.Components.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace Lampverket.Web.Tests.Pages;

public class ArendeTests
{
    private static BunitContext CreateCtx(IHandlaggareService service)
    {
        var ctx = new BunitContext();
        ctx.Services.AddSingleton(service);
        return ctx;
    }

    private static Core.Ansokan FakeAnsokan() => new()
    {
        Personnummer = "19900101-1234",
        Sokande = "Testaren",
        Arendetyp = Arendetyp.Tandning,
        BerordEnhet = "light.test",
        Motivering = "Det är mörkt.",
        OnskatDatum = DateOnly.FromDateTime(DateTime.Today)
    };

    // ── backlog #13: initial state ────────────────────────────────────────────
    [Fact]
    public void Arende_NoDecision_Shows_UnderHandlaggning()
    {
        var pending = new FakeArendeService(new Core.Arende
        {
            Diarienummer = "LV-2026-000001",
            Mottaget = DateTimeOffset.UtcNow,
            Ansokan = FakeAnsokan(),
            Status = Arendestatus.UnderHandlaggning
        });
        using var ctx = CreateCtx(pending);
        var cut = ctx.Render<ArendeDetalj>(p =>
            p.Add(x => x.Diarienummer, "LV-2026-000001"));
        Assert.Contains("handläggning", cut.Markup.ToLowerInvariant());
    }

    [Fact]
    public void Arende_NoDecision_Shows_Diarienummer()
    {
        var pending = new FakeArendeService(new Core.Arende
        {
            Diarienummer = "LV-2026-000099",
            Mottaget = DateTimeOffset.UtcNow,
            Ansokan = FakeAnsokan(),
            Status = Arendestatus.UnderHandlaggning
        });
        using var ctx = CreateCtx(pending);
        var cut = ctx.Render<ArendeDetalj>(p =>
            p.Add(x => x.Diarienummer, "LV-2026-000099"));
        Assert.Contains("LV-2026-000099", cut.Markup);
    }

    // ── backlog #14: beslut card renders ─────────────────────────────────────
    [Fact]
    public void Arende_WithBeslut_RendersBeslutCard()
    {
        var decided = new FakeArendeService(new Core.Arende
        {
            Diarienummer = "LV-2026-000001",
            Mottaget = DateTimeOffset.UtcNow,
            Ansokan = FakeAnsokan(),
            Status = Arendestatus.Verkstallt,
            Beslut = new Core.Beslut
            {
                Beslutstyp = Beslutstyp.Bifall,
                Beslutstext = "Lampverket beviljar ansökan.",
                Motivering = "Berättigat behov.",
                Lagrum = ["7 § lagen (2026:1)"],
                Overklagandehanvisning = "Kan överklagas inom 3 veckor.",
                Verkstallighet = "Belysningen tändes kl. 21:42.",
                Datum = DateTimeOffset.UtcNow
            }
        });
        using var ctx = CreateCtx(decided);
        var cut = ctx.Render<ArendeDetalj>(p =>
            p.Add(x => x.Diarienummer, "LV-2026-000001"));
        Assert.NotNull(cut.Find(".beslut-card"));
        Assert.Contains("Lampverket beviljar", cut.Markup);
    }

    [Fact]
    public void Arende_WithBeslut_ShowsBeslutstypLabel()
    {
        var decided = new FakeArendeService(new Core.Arende
        {
            Diarienummer = "LV-2026-000001",
            Mottaget = DateTimeOffset.UtcNow,
            Ansokan = FakeAnsokan(),
            Status = Arendestatus.Verkstallt,
            Beslut = new Core.Beslut
            {
                Beslutstyp = Beslutstyp.Bifall,
                Beslutstext = "Lampverket beviljar ansökan.",
                Motivering = "OK.",
                Lagrum = [],
                Overklagandehanvisning = "Kan överklagas.",
                Verkstallighet = "Utfört.",
                Datum = DateTimeOffset.UtcNow
            }
        });
        using var ctx = CreateCtx(decided);
        var cut = ctx.Render<ArendeDetalj>(p =>
            p.Add(x => x.Diarienummer, "LV-2026-000001"));
        Assert.Contains("Bifall", cut.Markup);
    }
}

file sealed class FakeArendeService : IHandlaggareService
{
    private readonly Core.Arende _arende;
    public FakeArendeService(Core.Arende arende) => _arende = arende;

    public Task<Core.Arende> RegisterAnsokanAsync(Core.Ansokan ansokan) =>
        throw new NotImplementedException();

    public Task<Core.Arende?> HamtaArendeAsync(string diarienummer) =>
        Task.FromResult<Core.Arende?>(diarienummer == _arende.Diarienummer ? _arende : null);
}
