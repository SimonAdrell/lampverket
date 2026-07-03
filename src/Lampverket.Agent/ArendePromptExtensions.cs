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
    }
}

