using Lampverket.Core;

namespace Lampverket.Agent;

public sealed class BeslutSlot
{
    private Beslut? _beslut;
    private int _verkstallighetsutfall; // 0 = inget försök, 1 = lyckad, 2 = misslyckad

    public Beslut? Beslut => _beslut;

    public bool TrySet(Beslut beslut) =>
        Interlocked.CompareExchange(ref _beslut, beslut, null) is null;

    // Sista försöket vinner: en retry som lyckas efter ett fel innebär att åtgärden faktiskt utfördes.
    // Till skillnad från beslutet (en oföränderlig rättsakt) är utfallet en observation som kan uppdateras.
    public void RegistreraVerkstallighetsforsok(bool lyckat) =>
        Interlocked.Exchange(ref _verkstallighetsutfall, lyckat ? 1 : 2);

    public Verkstallighetsstatus? Verkstallighetsutfall => _verkstallighetsutfall switch
    {
        1 => Verkstallighetsstatus.Verkstalld,
        2 => Verkstallighetsstatus.Misslyckad,
        _ => null,
    };
}
