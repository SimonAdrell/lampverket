namespace Lampverket.Core;

/// <summary>
/// Utfallet av verkställigheten — observerat av C# efter att beslutet fattats, skilt från
/// själva <see cref="Beslut"/> (som är handläggarens oföränderliga rättsakt).
/// </summary>
public enum Verkstallighetsstatus
{
    /// <summary>Ett verkställande HA-anrop gjordes och lyckades.</summary>
    Verkstalld,

    /// <summary>Ett verkställande HA-anrop gjordes men det sista försöket misslyckades.</summary>
    Misslyckad,

    /// <summary>Beslutet medger verkställighet men inget anrop gjordes (enheten redan i önskat läge).</summary>
    EjPakallad
}
