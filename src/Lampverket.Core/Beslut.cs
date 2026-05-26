namespace Lampverket.Core;

public sealed record Beslut
{
    public required Beslutstyp Beslutstyp { get; init; }
    public required string Beslutstext { get; init; }
    public required string Motivering { get; init; }
    public required string[] Lagrum { get; init; }
    public required string Overklagandehanvisning { get; init; }
    public required string Verkstallighet { get; init; }
    public required DateTimeOffset Datum { get; init; }
}
