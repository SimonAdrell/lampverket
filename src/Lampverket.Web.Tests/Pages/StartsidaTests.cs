using Bunit;
using Lampverket.Web.Components.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace Lampverket.Web.Tests.Pages;

public class StartsidaTests
{
    private static BunitContext CreateCtx()
    {
        var ctx = new BunitContext();
        ctx.Services.AddSingleton(TimeProvider.System);
        return ctx;
    }

    [Fact]
    public void Startsida_Renders_AuthorityName()
    {
        using var ctx = CreateCtx();
        var cut = ctx.Render<Startsida>();
        Assert.Contains("Lampverket", cut.Find("h1").TextContent);
    }

    [Fact]
    public void Startsida_Renders_Authority_Subtitle()
    {
        using var ctx = CreateCtx();
        var cut = ctx.Render<Startsida>();
        Assert.Contains("Myndigheten för Hemautomation", cut.Markup);
    }
}
