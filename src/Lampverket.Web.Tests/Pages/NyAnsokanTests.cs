using Bunit;
using Lampverket.Core;
using Lampverket.Web.Components.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace Lampverket.Web.Tests.Pages;

public class NyAnsokanTests
{
    private static BunitContext CreateCtx(IHandlaggareService? handlaggare = null)
    {
        var ctx = new BunitContext();
        ctx.Services.AddSingleton<IHandlaggareService>(
            handlaggare ?? new FakeHandlaggareService());
        ctx.Services.AddScoped<IUserSession, FakeUserSession>();
        return ctx;
    }

    // ── backlog #4: route renders a form ────────────────────────────────────
    [Fact]
    public void NyAnsokan_Renders_Form()
    {
        using var ctx = CreateCtx();
        var cut = ctx.Render<NyAnsokan>();
        Assert.NotNull(cut.Find("form"));
    }

    // ── backlog #5: personnummer validation ──────────────────────────────────
    [Fact]
    public void NyAnsokan_InvalidPersonnummer_ShowsError()
    {
        using var ctx = CreateCtx();
        var cut = ctx.Render<NyAnsokan>();
        var input = cut.Find("input[id='personnummer']");
        input.Change("not-valid");
        cut.Find("button[type='submit']").Click();
        Assert.Contains("ÅÅÅÅMMDD-XXXX", cut.Markup);
    }

    [Fact]
    public void NyAnsokan_ValidPersonnummer_NoFormatError()
    {
        using var ctx = CreateCtx();
        var cut = ctx.Render<NyAnsokan>();
        var input = cut.Find("input[id='personnummer']");
        input.Change("19900101-1234");
        Assert.Empty(cut.FindAll("span.field-error"));
    }

    [Fact]
    public void NyAnsokan_InvalidDatePersonnummer_ShowsError()
    {
        // Structurally valid but calendar-invalid date (month 99)
        using var ctx = CreateCtx();
        var cut = ctx.Render<NyAnsokan>();
        cut.Find("input[id='personnummer']").Change("20009999-1234");
        cut.Find("button[type='submit']").Click();
        Assert.Contains("ogiltigt datum", cut.Markup);
    }

    [Fact]
    public void NyAnsokan_Media_EmptySokterm_ShowsError()
    {
        using var ctx = CreateCtx();
        var cut = ctx.Render<NyAnsokan>();
        cut.Find("select[id='arendetyp']").Change(Arendetyp.Media.ToString());
        // leave sökterm blank
        cut.Find("input[id='personnummer']").Change("19900101-1234");
        cut.Find("textarea[id='motivering']").Change("Vill lyssna på musik.");
        cut.Find("input[id='forsäkran']").Change(true);
        cut.Find("button[type='submit']").Click();
        Assert.Contains("Sökterm krävs", cut.Markup);
    }

    // ── backlog #6: ärendetyp dropdown ───────────────────────────────────────
    [Fact]
    public void NyAnsokan_ArendetypDropdown_HasFiveOptions()
    {
        using var ctx = CreateCtx();
        var cut = ctx.Render<NyAnsokan>();
        var options = cut.FindAll("select[id='arendetyp'] option");
        Assert.Equal(5, options.Count);
    }

    // ── backlog #7: berörd enhet dropdown from IUserSession/config ───────────
    [Fact]
    public void NyAnsokan_BerordEnhetDropdown_ShowsDevices()
    {
        using var ctx = CreateCtx();
        var cut = ctx.Render<NyAnsokan>();
        var options = cut.FindAll("select[id='berord-enhet'] option");
        Assert.True(options.Count > 0, "Device dropdown should have at least one option");
    }

    // ── backlog #8: conditional önskad åtgärd ───────────────────────────────
    [Fact]
    public void NyAnsokan_Ljusstyrka_ShowsRangeInput()
    {
        using var ctx = CreateCtx();
        var cut = ctx.Render<NyAnsokan>();
        var dropdown = cut.Find("select[id='arendetyp']");
        dropdown.Change(Arendetyp.Ljusstyrka.ToString());
        Assert.NotNull(cut.Find("input[type='range'][id='onskad-atgard-range']"));
    }

    [Fact]
    public void NyAnsokan_Tandning_NoRangeInput()
    {
        using var ctx = CreateCtx();
        var cut = ctx.Render<NyAnsokan>();
        var dropdown = cut.Find("select[id='arendetyp']");
        dropdown.Change(Arendetyp.Tandning.ToString());
        Assert.Empty(cut.FindAll("input[type='range'][id='onskad-atgard-range']"));
    }

    [Fact]
    public void NyAnsokan_Media_ShowsTextInput()
    {
        using var ctx = CreateCtx();
        var cut = ctx.Render<NyAnsokan>();
        var dropdown = cut.Find("select[id='arendetyp']");
        dropdown.Change(Arendetyp.Media.ToString());
        Assert.NotNull(cut.Find("input[type='text'][id='onskad-atgard-text']"));
    }

    // ── backlog #9: motivering required ──────────────────────────────────────
    [Fact]
    public void NyAnsokan_EmptyMotivering_ShowsError()
    {
        using var ctx = CreateCtx();
        var cut = ctx.Render<NyAnsokan>();
        cut.Find("button[type='submit']").Click();
        Assert.Contains("Motivering krävs", cut.Markup);
    }

    // ── backlog #10: försäkran required ──────────────────────────────────────
    [Fact]
    public void NyAnsokan_UncheckedForsäkran_ShowsError()
    {
        using var ctx = CreateCtx();
        var cut = ctx.Render<NyAnsokan>();
        cut.Find("button[type='submit']").Click();
        Assert.Contains("försäkran", cut.Markup.ToLowerInvariant());
    }

    // ── backlog #11: valid submit calls service ───────────────────────────────
    [Fact]
    public async Task NyAnsokan_ValidSubmit_CallsHandlaggareService()
    {
        var fake = new FakeHandlaggareService();
        using var ctx = CreateCtx(fake);
        var cut = ctx.Render<NyAnsokan>();

        cut.Find("input[id='personnummer']").Change("19900101-1234");
        cut.Find("select[id='arendetyp']").Change(Arendetyp.Tandning.ToString());
        cut.Find("select[id='berord-enhet']").Change("light.test");
        cut.Find("textarea[id='motivering']").Change("Det är mörkt.");
        cut.Find("input[id='forsäkran']").Change(true);
        cut.Find("button[type='submit']").Click();

        await Task.Delay(50);
        Assert.True(fake.WasCalled, "IHandlaggareService.RegisterAnsokanAsync should have been called");
    }
}

// ── Fakes ────────────────────────────────────────────────────────────────────

file sealed class FakeHandlaggareService : IHandlaggareService
{
    public bool WasCalled { get; private set; }

    public Task<Arende> RegisterAnsokanAsync(Ansokan ansokan)
    {
        WasCalled = true;
        var arende = new Arende
        {
            Diarienummer = "LV-2026-000001",
            Mottaget = DateTimeOffset.UtcNow,
            Ansokan = ansokan,
            Status = Arendestatus.Inkommet
        };
        return Task.FromResult(arende);
    }

    public Task<Arende?> HamtaArendeAsync(string diarienummer) =>
        Task.FromResult<Arende?>(null);
}

file sealed class FakeUserSession : IUserSession
{
    public string? Namn { get; set; } = "Testaren";
}
