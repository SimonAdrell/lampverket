using Bunit;
using Lampverket.Web.Components.Pages;

namespace Lampverket.Web.Tests.Pages;

public class TillganglighetTests
{
    // ── backlog T2 #23: tillgänglighetsredogörelse page ──────────────────────
    [Fact]
    public void Tillganglighet_Renders_Page()
    {
        using var ctx = new BunitContext();
        var cut = ctx.Render<Tillganglighet>();
        Assert.NotNull(cut.Find("h1"));
    }

    [Fact]
    public void Tillganglighet_Contains_Authority_Name()
    {
        using var ctx = new BunitContext();
        var cut = ctx.Render<Tillganglighet>();
        Assert.Contains("Lampverket", cut.Markup);
    }
}
