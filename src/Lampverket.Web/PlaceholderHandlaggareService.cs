using Lampverket.Core;

namespace Lampverket.Web;

// Placeholder until Lampverket.Agent is wired in.
// Returns a "bordläggs" decision so the UI flow can be exercised end-to-end.
internal sealed class PlaceholderHandlaggareService : IHandlaggareService
{
    private static int _counter;

    public Task<Arende> RegisterAnsokanAsync(Ansokan ansokan)
    {
        var nr = Interlocked.Increment(ref _counter);
        var diarienummer = $"LV-{DateTime.Now.Year}-{nr:D6}";

        var arende = new Arende
        {
            Diarienummer = diarienummer,
            Mottaget = DateTimeOffset.Now,
            Ansokan = ansokan,
            Status = Arendestatus.Beslutat,
            Beslut = new Beslut
            {
                Beslutstyp = Beslutstyp.Avslag,
                Beslutstext = "Lampverket bordlägger ärendet.",
                Motivering = "Handläggaragenten är ännu ej driftsatt. Ärendet bordläggs tills vidare.",
                Lagrum = [],
                Overklagandehanvisning = "Detta beslut kan ej överklagas.",
                Verkstallighet = "Ingen åtgärd vidtagen.",
                Datum = DateTimeOffset.Now
            }
        };
        return Task.FromResult(arende);
    }

    public Task<Arende?> HamtaArendeAsync(string diarienummer) =>
        Task.FromResult<Arende?>(null);
}
