using Bunit;
using Lampverket.Web.Components.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace Lampverket.Web.Tests.Pages;

public class StartsidaBannerTests
{
    // ── backlog T2 #18-19: driftinfo banner wired to TimeProvider ────────────
    [Fact]
    public void Startsida_FikahelgdTime_Shows_DriftinfoBanner()
    {
        // Friday 14:30 UTC — FakeTimeProvider.LocalTimeZone = Utc so hour = 14
        var friday1430 = new DateTimeOffset(2026, 5, 22, 14, 30, 0, TimeSpan.Zero);
        using var ctx = new BunitContext();
        ctx.Services.AddSingleton<TimeProvider>(new FakeTimeProvider(friday1430));

        var cut = ctx.Render<Startsida>();
        Assert.NotNull(cut.Find(".driftinfo-banner"));
        Assert.Contains("fika", cut.Markup.ToLowerInvariant());
    }

    [Fact]
    public void Startsida_NormalTime_NoDriftinfoBanner()
    {
        // Monday 10:00
        var monday1000 = new DateTimeOffset(2026, 5, 25, 10, 0, 0, TimeSpan.Zero);
        using var ctx = new BunitContext();
        ctx.Services.AddSingleton<TimeProvider>(new FakeTimeProvider(monday1000));

        var cut = ctx.Render<Startsida>();
        Assert.Empty(cut.FindAll(".driftinfo-banner"));
    }

    [Fact]
    public void Startsida_Friday_15h_NoDriftinfoBanner()
    {
        // Friday 15:00 — fika over
        var friday1500 = new DateTimeOffset(2026, 5, 22, 15, 0, 0, TimeSpan.Zero);
        using var ctx = new BunitContext();
        ctx.Services.AddSingleton<TimeProvider>(new FakeTimeProvider(friday1500));

        var cut = ctx.Render<Startsida>();
        Assert.Empty(cut.FindAll(".driftinfo-banner"));
    }
}

file sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow.ToUniversalTime();
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}
