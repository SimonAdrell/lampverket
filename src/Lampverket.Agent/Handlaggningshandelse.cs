using Lampverket.Core;

namespace Lampverket.Agent;

/// <summary>
/// En händelse som handläggaragenten rapporterar under loopens gång (via <see cref="IProgress{T}"/>)
/// så att den väntande sidan kan uppdateras innan hela loopen är klar: antingen ett fattat
/// <see cref="Core.Beslut"/> (visas direkt, ~8 s före loopens slut) eller ett transient
/// handläggningssteg (t.ex. "Granskar hemförhållanden").
/// </summary>
public sealed record Handlaggningshandelse
{
    public string? Steg { get; private init; }
    public Beslut? Beslut { get; private init; }
    public string? MotiveringUtkast { get; private init; }

    public static Handlaggningshandelse ForSteg(string steg) => new() { Steg = steg };
    public static Handlaggningshandelse ForBeslut(Beslut beslut) => new() { Beslut = beslut };
    public static Handlaggningshandelse ForMotiveringUtkast(string motivering) => new() { MotiveringUtkast = motivering };
}
