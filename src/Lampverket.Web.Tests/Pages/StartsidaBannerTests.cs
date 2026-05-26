using Bunit;
using Lampverket.Web.Components.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace Lampverket.Web.Tests.Pages;

public class StartsidaBannerTests
{
    // ── backlog T2 #18-19: driftinfo banner wired to TimeProvider ────────────
    // Startsida.razor uses Clock.GetUtcNow() + TimeZoneInfo "Europe/Stockholm",
    // so test times are given as UTC equivalents of Stockholm CEST (UTC+2).

    [Fact]
    public void Startsida_FikahelgdTime_Shows_DriftinfoBanner()
    {
        // Friday 14:30 Stockholm CEST = 12:30 UTC
        var friday1430Stockholm = new DateTimeOffset(2026, 5, 22, 12, 30, 0, TimeSpan.Zero);
        using var ctx = new BunitContext();
        ctx.Services.AddSingleton<TimeProvider>(new FakeTimeProvider(friday1430Stockholm));

        var cut = ctx.Render<Startsida>();
        Assert.NotNull(cut.Find(".driftinfo-banner"));
        Assert.Contains("fika", cut.Markup.ToLowerInvariant());
    }

    [Fact]
    public void Startsida_NormalTime_NoDriftinfoBanner()
    {
        // Monday 10:00 Stockholm CEST = 08:00 UTC
        var monday1000Stockholm = new DateTimeOffset(2026, 5, 25, 8, 0, 0, TimeSpan.Zero);
        using var ctx = new BunitContext();
        ctx.Services.AddSingleton<TimeProvider>(new FakeTimeProvider(monday1000Stockholm));

        var cut = ctx.Render<Startsida>();
        Assert.Empty(cut.FindAll(".driftinfo-banner"));
    }

    [Fact]
    public void Startsida_Friday_15h_NoDriftinfoBanner()
    {
        // Friday 15:00 Stockholm CEST (fika over) = 13:00 UTC
        var friday1500Stockholm = new DateTimeOffset(2026, 5, 22, 13, 0, 0, TimeSpan.Zero);
        using var ctx = new BunitContext();
        ctx.Services.AddSingleton<TimeProvider>(new FakeTimeProvider(friday1500Stockholm));

        var cut = ctx.Render<Startsida>();
        Assert.Empty(cut.FindAll(".driftinfo-banner"));
    }
}

file sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow.ToUniversalTime();
}
