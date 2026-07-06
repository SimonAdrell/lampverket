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

        public string BuildUserMessageWithLiveContext(string liveContext) => $"""
    {arende.BuildUserMessage()}

    Aktuellt hemtillstånd (GetLiveContext):
    {liveContext}

    Live context har hämtats av systemet före beslutsfasen. Anropa nu `lamna_beslut`
    med det formella beslutet.
    """;

        // Föreläggande i andra rundan: beslutet är redan fattat, så detta upphäver
        // VERKTYGSPROTOKOLL steg 1-2 (GetLiveContext + lamna_beslut) och går direkt till
        // verkställighet. Kontexten hämtades i fas 1 och kan vara några sekunder gammal.
        public string BuildVerkstallighetsnudge(string entityId, string beslutadAtgard) => $"""
    <verkstallighetsforelaggande>
        Diarienummer: {arende.Diarienummer}
        Beslut: bifall (redan utfärdat och registrerat i diariet).
        Berörd enhet: {entityId}
        Beslutad åtgärd: {beslutadAtgard}

        Ärendet är beslutat men den beslutade åtgärden har ännu inte verkställts.
        Verkställ nu: anropa den verkställande HA-funktionen (t.ex. HassTurnOn,
        HassLightSet) för enheten. Använd entity-id som `name`-parameter.

        Beslutet är redan fattat och live context är redan inhämtat av systemet.
        Anropa INTE lamna_beslut och anropa INTE GetLiveContext. Gå direkt till
        verkställighet och avsluta med en kort bekräftelse.
    </verkstallighetsforelaggande>
    """;
    }
}

