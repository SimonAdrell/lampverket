using Lampverket.Core;

namespace Lampverket.Agent.Tests;

public class VerkstallighetsnudgeTests
{
    private static Bifall Bifall() => new(
        "Beslut.", "Motivering.", [], "Kan överklagas.", "Åtgärd.", DateTimeOffset.UtcNow);

    private static DelvisBifall DelvisBifall() => new(
        "Beslut.", "Motivering.", [], "Kan överklagas.", "Åtgärd.", DateTimeOffset.UtcNow);

    private static Avslag Avslag() => new(
        "Beslut.", "Motivering.", [], "Kan överklagas.", DateTimeOffset.UtcNow);

    private static Avvisning Avvisning() => new(
        "Beslut.", "Motivering.", [], "Kan överklagas.", DateTimeOffset.UtcNow);

    private static Bordlaggning Bordlaggning() => new(
        "Beslut.", "Motivering.", [], "Kan överklagas.", DateTimeOffset.UtcNow);

    [Fact]
    public void Bifall_UtanForsok_Nudgas()
    {
        Assert.True(HandlaggareAgent.BehoverVerkstallighetsnudge(Bifall(), null));
    }

    [Fact]
    public void DelvisBifall_UtanForsok_Nudgas()
    {
        Assert.True(HandlaggareAgent.BehoverVerkstallighetsnudge(DelvisBifall(), null));
    }

    [Fact]
    public void Bifall_Verkstalld_NudgasInte()
    {
        Assert.False(HandlaggareAgent.BehoverVerkstallighetsnudge(Bifall(), Verkstallighetsstatus.Verkstalld));
    }

    [Fact]
    public void Bifall_Misslyckad_NudgasInte()
    {
        // Ett försök gjordes och misslyckades — Claude har redan sett felet; ingen omkörning.
        Assert.False(HandlaggareAgent.BehoverVerkstallighetsnudge(Bifall(), Verkstallighetsstatus.Misslyckad));
    }

    [Theory]
    [MemberData(nameof(IckeVerkstallbaraBeslut))]
    public void IckeVerkstallbartBeslut_NudgasInte(Beslut beslut)
    {
        Assert.False(HandlaggareAgent.BehoverVerkstallighetsnudge(beslut, null));
    }

    public static TheoryData<Beslut> IckeVerkstallbaraBeslut() =>
        [Avslag(), Avvisning(), Bordlaggning()];

    [Fact]
    public void IngetBeslut_NudgasInte()
    {
        Assert.False(HandlaggareAgent.BehoverVerkstallighetsnudge(null, null));
    }
}
