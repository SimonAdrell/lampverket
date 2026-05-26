using Bunit;
using Lampverket.Core;
using Lampverket.Web.Components.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace Lampverket.Web.Tests.Pages;

public class KvittensGagTests
{
    private static BunitContext CreateCtx()
    {
        var ctx = new BunitContext();
        ctx.Services.AddSingleton<IHandlaggareService>(new NullHandlaggare());
        return ctx;
    }

    // ── backlog T2 #21: köplats gag ──────────────────────────────────────────
    [Fact]
    public void Kvittens_Shows_Koplats()
    {
        using var ctx = CreateCtx();
        var cut = ctx.Render<Kvittens>(p =>
            p.Add(x => x.Diarienummer, "LV-2026-000001"));
        Assert.Contains("kön", cut.Markup.ToLowerInvariant());
    }

    [Fact]
    public void Kvittens_Shows_HandlaggningstidEstimate()
    {
        using var ctx = CreateCtx();
        var cut = ctx.Render<Kvittens>(p =>
            p.Add(x => x.Diarienummer, "LV-2026-000001"));
        Assert.Contains("handläggningstid", cut.Markup.ToLowerInvariant());
    }
}

file sealed class NullHandlaggare : IHandlaggareService
{
    public Task<Arende> RegisterAnsokanAsync(Ansokan ansokan) =>
        throw new NotImplementedException();
    public Task<Arende?> HamtaArendeAsync(string diarienummer) =>
        Task.FromResult<Arende?>(null);
}
