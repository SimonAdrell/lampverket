namespace Lampverket.Core;

public sealed record Arende(string Diarienummer, DateTimeOffset Mottaget, Ansokan Ansokan, Arendestatus Status, Beslut? Beslut = null, Verkstallighetsstatus? Verkstallighetsutfall = null);
