using Bunit;
using Lampverket.Web.Components.Pages;

namespace Lampverket.Web.Tests.Pages;

public class StartsidaTests
{
    [Fact]
    public void Startsida_Renders_AuthorityName()
    {
        using var ctx = new BunitContext();
        var cut = ctx.Render<Startsida>();
        Assert.Contains("Lampverket", cut.Find("h1").TextContent);
    }

    [Fact]
    public void Startsida_Renders_Authority_Subtitle()
    {
        using var ctx = new BunitContext();
        var cut = ctx.Render<Startsida>();
        Assert.Contains("Myndigheten för Hemautomation", cut.Markup);
    }
}
