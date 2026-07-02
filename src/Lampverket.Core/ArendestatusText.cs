namespace Lampverket.Core;

public static class ArendestatusText
{
    public static string Text(this Arendestatus s) => s switch
    {
        Arendestatus.Inkommet => "Inkommet",
        Arendestatus.Beslutat => "Beslutat",
        Arendestatus.Verkstallt => "Verkställt",
        Arendestatus.Bordlagt => "Bordlagt",
        _ => s.ToString()
    };

    // Matchar CSS-klasserna i app.css (.status-pill.<klass>).
    public static string CssKlass(this Arendestatus s) => s switch
    {
        Arendestatus.Inkommet => "inkommet",
        Arendestatus.Beslutat => "beslutat",
        Arendestatus.Verkstallt => "verkstallt",
        Arendestatus.Bordlagt => "bordlagt",
        _ => "inkommet"
    };
}
