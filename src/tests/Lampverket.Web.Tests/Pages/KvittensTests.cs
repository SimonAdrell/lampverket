using Bunit;
using Lampverket.Core;
using Lampverket.Web.Components.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace Lampverket.Web.Tests.Pages;

public class KvittensTests
{
    private static BunitContext CreateCtx()
    {
        var ctx = new BunitContext();
        ctx.Services.AddSingleton<IAnsokanService>(new FakeHandlaggareForKvittens());
        return ctx;
    }

    [Fact]
    public void Kvittens_Shows_Diarienummer()
    {
        using var ctx = CreateCtx();
        var cut = ctx.Render<Kvittens>(p => p.Add(x => x.Diarienummer, "LV-2026-000042"));
        Assert.Contains("LV-2026-000042", cut.Markup);
    }

    [Fact]
    public void Kvittens_Shows_Confirmation_Text()
    {
        using var ctx = CreateCtx();
        var cut = ctx.Render<Kvittens>(p => p.Add(x => x.Diarienummer, "LV-2026-000042"));
        Assert.Contains("mottagits", cut.Markup.ToLowerInvariant());
    }
}

file sealed class FakeHandlaggareForKvittens : IAnsokanService
{
    public Task<Arende> RegisterAnsokanAsync(Ansokan ansokan) =>
        throw new NotImplementedException();
    public Task<Arende?> HamtaArendeAsync(string diarienummer) =>
        Task.FromResult<Arende?>(null);
}
