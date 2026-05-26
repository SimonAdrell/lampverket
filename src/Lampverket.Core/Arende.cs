namespace Lampverket.Core;

public sealed record Arende
{
    public required string Diarienummer { get; init; }
    public required DateTimeOffset Mottaget { get; init; }
    public required Ansokan Ansokan { get; init; }
    public required Arendestatus Status { get; init; }
    public Beslut? Beslut { get; init; }
}
