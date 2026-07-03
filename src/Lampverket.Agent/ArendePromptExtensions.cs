using Lampverket.Core;

namespace Lampverket.Agent;

internal static class ArendePromptExtensions
{
    extension(Arende arende)
    {
        public string BuildUserMessage() => $"""
    <arende>
        Diarienummer: {arende.Diarienummer}
        Sökande: {arende.Ansokan.Sokande}
        Ärendetyp: {arende.Ansokan.Arendetyp.Text()}
        Önskad åtgärd: {arende.Ansokan.OnskadAtgard ?? "(ej angiven)"}
        Motivering: {arende.Ansokan.Motivering}
        Önskat datum: {arende.Ansokan.OnskatDatum:yyyy-MM-dd}
        Ärende mottaget datum: {arende.Mottaget}
        <enhet>
            EntityID: {arende.Ansokan.BerordEnhet.EntityId}
            Namn: {arende.Ansokan.BerordEnhet.FriendlyName}
        </enhet>
    </arende>
        Handlägg ärendet enligt verktygsprotokollet.
    """;

        // Föreläggande i andra rundan: beslutet är redan fattat, så detta upphäver
        // VERKTYGSPROTOKOLL steg 2 (lamna_beslut) och går direkt till verkställighet.
        public string BuildVerkstallighetsnudge(string entityId, string beslutadAtgard) => $"""
    <verkstallighetsforelaggande>
        Diarienummer: {arende.Diarienummer}
        Beslut: bifall (redan utfärdat och registrerat i diariet).
        Berörd enhet: {entityId}
        Beslutad åtgärd: {beslutadAtgard}

        Ärendet är beslutat men den beslutade åtgärden har ännu inte verkställts.
        Verkställ nu: anropa den verkställande HA-funktionen (t.ex. HassTurnOn,
        HassLightSet) för enheten. Använd entity-id som `name`-parameter.

        Beslutet är redan fattat — anropa INTE lamna_beslut igen. Gå direkt till
        verkställighet och avsluta med en kort bekräftelse.
    </verkstallighetsforelaggande>
    """;
    }
}

