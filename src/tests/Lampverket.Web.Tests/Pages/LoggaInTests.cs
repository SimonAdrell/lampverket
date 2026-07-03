using Bunit;
using Lampverket.Core;
using Lampverket.Web.Components.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace Lampverket.Web.Tests.Pages;

public class LoggaInTests
{
    private static BunitContext CreateCtx()
    {
        var ctx = new BunitContext();
        ctx.Services.AddScoped<IUserSession, FakeSessionForLogin>();
        return ctx;
    }

    // ── backlog T2 #20: BankID gag page ──────────────────────────────────────
    [Fact]
    public void LoggaIn_Renders_BankID_UI()
    {
        using var ctx = CreateCtx();
        var cut = ctx.Render<LoggaIn>();
        Assert.Contains("BankID", cut.Markup);
    }

    [Fact]
    public void LoggaIn_Has_NameInput()
    {
        using var ctx = CreateCtx();
        var cut = ctx.Render<LoggaIn>();
        Assert.NotNull(cut.Find("input[id='namn']"));
    }

    [Fact]
    public void LoggaIn_Submit_SetsSessionNamn()
    {
        using var ctx = CreateCtx();
        var session = ctx.Services.GetRequiredService<IUserSession>() as FakeSessionForLogin;
        var cut = ctx.Render<LoggaIn>();
        cut.Find("input[id='namn']").Change("Simon");
        cut.Find("button[type='submit']").Click();
        Assert.Equal("Simon", session!.Namn);
    }
}

file sealed class FakeSessionForLogin : IUserSession
{
    public string? Namn { get; set; }
}
