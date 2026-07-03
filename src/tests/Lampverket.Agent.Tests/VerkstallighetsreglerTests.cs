using Lampverket.Core;

namespace Lampverket.Agent.Tests;

public class VerkstallighetsreglerTests
{
    private static Bifall Bifall() => new(
        "Beslut.", "Motivering.", [], "Kan överklagas.", "Åtgärd.", DateTimeOffset.UtcNow);

    private static Avslag Avslag() => new(
        "Beslut.", "Motivering.", [], "Kan överklagas.", DateTimeOffset.UtcNow);

    private static Bordlaggning Bordlaggning() => new(
        "Beslut.", "Motivering.", [], "Kan överklagas.", DateTimeOffset.UtcNow);

    [Fact]
    public void Bifall_Verkstalld_GerVerkstallt()
    {
        var (status, utfall) = Verkstallighetsregler.Avgor(Bifall(), Verkstallighetsstatus.Verkstalld);
        Assert.Equal(Arendestatus.Verkstallt, status);
        Assert.Equal(Verkstallighetsstatus.Verkstalld, utfall);
    }

    [Fact]
    public void Bifall_IngetForsok_GerVerkstalltEjPakallad()
    {
        var (status, utfall) = Verkstallighetsregler.Avgor(Bifall(), null);
        Assert.Equal(Arendestatus.Verkstallt, status);
        Assert.Equal(Verkstallighetsstatus.EjPakallad, utfall);
    }

    [Fact]
    public void Bifall_Misslyckad_GerBeslutat()
    {
        var (status, utfall) = Verkstallighetsregler.Avgor(Bifall(), Verkstallighetsstatus.Misslyckad);
        Assert.Equal(Arendestatus.Beslutat, status);
        Assert.Equal(Verkstallighetsstatus.Misslyckad, utfall);
    }

    [Fact]
    public void Avslag_GerBeslutatUtanVerkstallighet()
    {
        var (status, utfall) = Verkstallighetsregler.Avgor(Avslag(), null);
        Assert.Equal(Arendestatus.Beslutat, status);
        Assert.Null(utfall);
    }

    [Fact]
    public void Bordlaggning_GerBordlagtUtanVerkstallighet()
    {
        var (status, utfall) = Verkstallighetsregler.Avgor(Bordlaggning(), null);
        Assert.Equal(Arendestatus.Bordlagt, status);
        Assert.Null(utfall);
    }

    [Fact]
    public void IckeVerkstallbart_IgnorerarUtfall()
    {
        // Defensivt: guarden ska aldrig registrera ett utfall för ett avslag, men om det ändå
        // skedde får det inte påverka statusen.
        var (status, utfall) = Verkstallighetsregler.Avgor(Avslag(), Verkstallighetsstatus.Misslyckad);
        Assert.Equal(Arendestatus.Beslutat, status);
        Assert.Null(utfall);
    }
}
