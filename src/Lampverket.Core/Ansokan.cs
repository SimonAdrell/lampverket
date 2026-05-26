namespace Lampverket.Core;

public sealed record Ansokan
{
    public required string Personnummer { get; init; }
    public required string Sokande { get; init; }
    public required Arendetyp Arendetyp { get; init; }
    public required string BerordEnhet { get; init; }
    public string? OnskadAtgard { get; init; }
    public required string Motivering { get; init; }
    public required DateOnly OnskatDatum { get; init; }
}
