using Bunit;
using Lampverket.Core;
using Lampverket.Web.Components.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace Lampverket.Web.Tests.Pages;

public class ArendeAppealTests
{
    private static Core.Ansokan FakeAnsokan() => new()
    {
        Personnummer = "19900101-1234",
        Sokande = "Testaren",
        Arendetyp = Arendetyp.Tanding,
        BerordEnhet = new Enhet("light.test", "Testlampa"),
        Motivering = "Test.",
        OnskatDatum = DateOnly.FromDateTime(DateTime.Today)
    };

    // ── backlog T2 #25: överklagande button on avslag ────────────────────────
    [Fact]
    public void Arende_Avslag_Shows_Appeal_Button()
    {
        var decided = new FakeArendeServiceForAppeal(new Core.Arende(
            Diarienummer: "LV-2026-000001",
            Mottaget: DateTimeOffset.UtcNow,
            Ansokan: FakeAnsokan(),
            Status: Arendestatus.Beslutat,
            Beslut: new Core.Avslag(
                Beslutstext: "Avslaget.",
                Motivering: "Obehövligt.",
                Lagrum: [],
                Overklagandehanvisning: "Kan överklagas.",
                Datum: DateTimeOffset.UtcNow)
        ));
        using var ctx = new BunitContext();
        ctx.Services.AddSingleton<IAnsokanService>(decided);
        ctx.Services.AddSingleton<IArendeNotifier, ArendeNotifier>();
        var cut = ctx.Render<ArendeDetalj>(p =>
            p.Add(x => x.Diarienummer, "LV-2026-000001"));
        Assert.NotNull(cut.Find(".overklaga-btn"));
    }

    [Fact]
    public void Arende_Bifall_NoAppealButton()
    {
        var decided = new FakeArendeServiceForAppeal(new Core.Arende(
            Diarienummer: "LV-2026-000001",
            Mottaget: DateTimeOffset.UtcNow,
            Ansokan: FakeAnsokan(),
            Status: Arendestatus.Verkstallt,
            Beslut: new Core.Bifall(
                Beslutstext: "Beviljat.",
                Motivering: "OK.",
                Lagrum: [],
                Overklagandehanvisning: "Kan överklagas.",
                VerkstallighetsText: "Utfört.",
                Datum: DateTimeOffset.UtcNow)
        ));
        using var ctx = new BunitContext();
        ctx.Services.AddSingleton<IAnsokanService>(decided);
        ctx.Services.AddSingleton<IArendeNotifier, ArendeNotifier>();
        var cut = ctx.Render<ArendeDetalj>(p =>
            p.Add(x => x.Diarienummer, "LV-2026-000001"));
        Assert.Empty(cut.FindAll(".overklaga-btn"));
    }
}

file sealed class FakeArendeServiceForAppeal(Core.Arende arende) : IAnsokanService
{
    public Task<Core.Arende> RegisterAnsokanAsync(Core.Ansokan ansokan) =>
        throw new NotImplementedException();
    public Task<Core.Arende?> HamtaArendeAsync(string diarienummer) =>
        Task.FromResult<Core.Arende?>(arende);
}
