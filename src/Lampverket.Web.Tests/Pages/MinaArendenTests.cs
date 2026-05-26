using Bunit;
using Lampverket.Core;
using Lampverket.Web.Components.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace Lampverket.Web.Tests.Pages;

public class MinaArendenTests
{
    private static Core.Ansokan FakeAnsokan(string sokande) => new()
    {
        Personnummer = "19900101-1234",
        Sokande = sokande,
        Arendetyp = Arendetyp.Tandning,
        BerordEnhet = "light.test",
        Motivering = "Test.",
        OnskatDatum = DateOnly.FromDateTime(DateTime.Today)
    };

    // ── backlog T2 #22: Mina ärenden ─────────────────────────────────────────
    [Fact]
    public void MinaArenden_EmptyDiariet_Shows_NoArenden()
    {
        using var ctx = new BunitContext();
        ctx.Services.AddSingleton<IDiariet>(new FakeDiariet([]));
        var cut = ctx.Render<MinaArenden>();
        Assert.DoesNotContain("LV-", cut.Markup);
    }

    [Fact]
    public void MinaArenden_WithArenden_Shows_Diarienummer()
    {
        var arenden = new[]
        {
            new Core.Arende
            {
                Diarienummer = "LV-2026-000001",
                Mottaget = DateTimeOffset.UtcNow,
                Ansokan = FakeAnsokan("Simon"),
                Status = Arendestatus.Verkstallt
            }
        };
        using var ctx = new BunitContext();
        ctx.Services.AddSingleton<IDiariet>(new FakeDiariet(arenden));
        var cut = ctx.Render<MinaArenden>();
        Assert.Contains("LV-2026-000001", cut.Markup);
    }

    [Fact]
    public void MinaArenden_Shows_Status()
    {
        var arenden = new[]
        {
            new Core.Arende
            {
                Diarienummer = "LV-2026-000002",
                Mottaget = DateTimeOffset.UtcNow,
                Ansokan = FakeAnsokan("Simon"),
                Status = Arendestatus.Inkommet
            }
        };
        using var ctx = new BunitContext();
        ctx.Services.AddSingleton<IDiariet>(new FakeDiariet(arenden));
        var cut = ctx.Render<MinaArenden>();
        Assert.Contains("Inkommet", cut.Markup);
    }
}

file sealed class FakeDiariet(IEnumerable<Core.Arende> arenden) : IDiariet
{
    private readonly IReadOnlyList<Core.Arende> _arenden = arenden.ToList();

    public Task AppendAsync(Core.Arende arende) => Task.CompletedTask;
    public Task<IReadOnlyList<Core.Arende>> HamtaAllaAsync() => Task.FromResult(_arenden);
    public Task<Core.Arende?> HamtaAsync(string diarienummer) =>
        Task.FromResult(_arenden.FirstOrDefault(a => a.Diarienummer == diarienummer));
}
