using Lampverket.Core;

namespace Lampverket.Agent;

public sealed class BeslutSlot
{
    private Beslut? _beslut;
    private int _verkstallighetsutfall; // 0 = inget försök, 1 = lyckad, 2 = misslyckad

    public Beslut? Beslut => _beslut;

    public bool TrySet(Beslut beslut) =>
        Interlocked.CompareExchange(ref _beslut, beslut, null) is null;

    // Bekräftad verkställighet vinner: en retry som lyckas efter ett fel innebär att åtgärden faktiskt
    // utfördes, men ett senare fel får inte skriva över en redan bekräftad sidoeffekt.
    public void RegistreraVerkstallighetsforsok(bool lyckat)
    {
        if (lyckat)
        {
            Interlocked.Exchange(ref _verkstallighetsutfall, 1);
            return;
        }

        Interlocked.CompareExchange(ref _verkstallighetsutfall, 2, 0);
    }

    public Verkstallighetsstatus? Verkstallighetsutfall => _verkstallighetsutfall switch
    {
        1 => Verkstallighetsstatus.Verkstalld,
        2 => Verkstallighetsstatus.Misslyckad,
        _ => null,
    };
}
