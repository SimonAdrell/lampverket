namespace Lampverket.Core;

/// <summary>
/// Stämmer av ett beslut mot det observerade verkställighetsutfallet och avgör ärendets
/// slutstatus. Ren och deterministisk — här bor invarianten "Verkställt ⇒ åtgärden bekräftad".
/// </summary>
public static class Verkstallighetsregler
{
    public static (Arendestatus Status, Verkstallighetsstatus? Verkstallighetsutfall) Avgor(
        Beslut beslut, Verkstallighetsstatus? utfall)
    {
        // Avslag, Avvisning, Bordläggning: ingen verkställighet väntas — statusen är beslutsfasens.
        if (!beslut.TillaterVerkstallighet)
        {
            return (beslut.ResulterandeStatus, null);
        }

        return utfall switch
        {
            // Anropet gjordes och sket sig: beslutet står kvar, men ärendet når aldrig Verkställt.
            Verkstallighetsstatus.Misslyckad => (Arendestatus.Beslutat, Verkstallighetsstatus.Misslyckad),
            // Anropet gjordes och lyckades.
            Verkstallighetsstatus.Verkstalld => (Arendestatus.Verkstallt, Verkstallighetsstatus.Verkstalld),
            // Inget anrop gjordes (enheten redan i önskat läge) — nedgradera inte.
            _ => (Arendestatus.Verkstallt, Verkstallighetsstatus.EjPakallad),
        };
    }
}
