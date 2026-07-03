namespace Lampverket.Core;

public enum Arendetyp { Tanding, Slackning, Ljusstyrka, Media, Volym }

public static class ArendetypText
{
    public static string Text(this Arendetyp t) => t switch
    {
        Arendetyp.Tanding => "Tändning av belysning",
        Arendetyp.Slackning => "Släckning av belysning",
        Arendetyp.Ljusstyrka => "Justering av ljusstyrka",
        Arendetyp.Media => "Uppspelning av media",
        Arendetyp.Volym => "Justering av volym",
        _ => throw new ArgumentOutOfRangeException(nameof(t)),
    };
}