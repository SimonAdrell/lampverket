namespace Lampverket.Core;

public abstract record Beslut(string Beslutstext, string Motivering, string[] Lagrum, string Overklagandehanvisning, DateTimeOffset Datum)
{
    public abstract Arendestatus ResulterandeStatus { get; }
    public virtual bool TillaterVerkstallighet => false;
    public virtual string Verkstallighet => "Ingen åtgärd vidtagen.";

    private const string StandardOverklagande =
        "Beslutet kan överklagas till Hemautomationsöverdomstolen inom tre veckor.";

    public static Beslut BordlaggOkandEnhet(DateTimeOffset now, string enhet)
        => new Bordlaggning("Lampverket bordlägger ärendet",
            $"Enheten \"{enhet}\" matchar inte myndighetens enhetskriterier. Ärendet bordläggs.",
            [], StandardOverklagande, now);

    public static Beslut BordlaggUtanBeslut(DateTimeOffset now)
        => new Bordlaggning("Lampverket bordlägger ärendet",
            "Handläggaragentens svar var ogiltigt eller uteblev. Ärendet bordläggs.",
            [], StandardOverklagande, now);

    public static Beslut BordlaggHandlaggningsfel(DateTimeOffset now)
        => new Bordlaggning("Lampverket bordlägger ärendet",
            "Handläggningsfel uppstod under ärendets beredning. Ärendet bordläggs.",
            [], StandardOverklagande, now);
}

public abstract record VerkstalltBeslut(string Beslutstext, string Motivering, string[] Lagrum, string Overklagandehanvisning, string VerkstallighetsText, DateTimeOffset Datum)
    : Beslut(Beslutstext, Motivering, Lagrum, Overklagandehanvisning, Datum)
{
    // Beslutsfasens status. Verkställt nås aldrig från beslutstypen ensam — bara via
    // Verkstallighetsregler efter att utfallet av HA-anropet observerats.
    public override Arendestatus ResulterandeStatus => Arendestatus.Beslutat;
    public override bool TillaterVerkstallighet => true;
    public override string Verkstallighet => VerkstallighetsText;
}

public sealed record Bifall(string Beslutstext, string Motivering, string[] Lagrum, string Overklagandehanvisning, string VerkstallighetsText, DateTimeOffset Datum)
    : VerkstalltBeslut(Beslutstext, Motivering, Lagrum, Overklagandehanvisning, VerkstallighetsText, Datum);

public sealed record DelvisBifall(string Beslutstext, string Motivering, string[] Lagrum, string Overklagandehanvisning, string VerkstallighetsText, DateTimeOffset Datum)
    : VerkstalltBeslut(Beslutstext, Motivering, Lagrum, Overklagandehanvisning, VerkstallighetsText, Datum);

public sealed record Avslag(string Beslutstext, string Motivering, string[] Lagrum, string Overklagandehanvisning, DateTimeOffset Datum)
    : Beslut(Beslutstext, Motivering, Lagrum, Overklagandehanvisning, Datum)
{
    public override Arendestatus ResulterandeStatus => Arendestatus.Beslutat;
}

public sealed record Avvisning(string Beslutstext, string Motivering, string[] Lagrum, string Overklagandehanvisning, DateTimeOffset Datum)
    : Beslut(Beslutstext, Motivering, Lagrum, Overklagandehanvisning, Datum)
{
    public override Arendestatus ResulterandeStatus => Arendestatus.Beslutat;
}

public sealed record Bordlaggning(string Beslutstext, string Motivering, string[] Lagrum, string Overklagandehanvisning, DateTimeOffset Datum)
    : Beslut(Beslutstext, Motivering, Lagrum, Overklagandehanvisning, Datum)
{
    public override Arendestatus ResulterandeStatus => Arendestatus.Bordlagt;
}
