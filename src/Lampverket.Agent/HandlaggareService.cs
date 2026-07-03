using System.Diagnostics;
using System.Threading.Channels;
using Lampverket.Core;
using Microsoft.Extensions.Logging;

namespace Lampverket.Agent;

public sealed class HandlaggareService(IDiariet diariet, IHandlaggareAgent agent, TimeProvider clock,
    IArendeNotifier notifier, ChannelWriter<string> queue, ILogger<HandlaggareService> logger) : IAnsokanService, IArendeProcessor
{
    private static readonly TimeOnly FikaStart = new(14, 30);
    private static readonly TimeOnly FikaSlut = new(15, 0);

    private static readonly TimeZoneInfo _stockholmTz =
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");

    private readonly IDiariet _diariet = diariet;
    private readonly IHandlaggareAgent _agent = agent;
    private readonly TimeProvider _clock = clock;
    private readonly IArendeNotifier _notifier = notifier;
    private readonly ChannelWriter<string> _queue = queue;
    private readonly ILogger<HandlaggareService> _logger = logger;

    public async Task<Arende> RegisterAnsokanAsync(Ansokan ansokan)
    {
        var now = _clock.GetUtcNow();
        var diarienummer = await _diariet.AllokeraDiarienummerAsync(now.Year);
        var arende = new Arende(diarienummer, now, ansokan, Arendestatus.Inkommet);

        await _diariet.AppendAsync(arende);
        await _queue.WriteAsync(diarienummer);

        return arende;
    }

    public async Task ProcessArendeAsync(string diarienummer, CancellationToken ct = default)
    {
        var arende = await _diariet.HamtaAsync(diarienummer);

        if (arende is null)
        {
            _logger.LogWarning("Ärende {Diarienummer} saknas i diariet; handläggning avbryts.", diarienummer);
            return;
        }

        // Omslutande spann: nästlar Anthropic-anropen till en enda trace och bär steg-events (via
        // Rapportera) så beslutet syns ~8 s före loopens slut direkt i Aspire.
        using var activity = AgentDiagnostics.Source.StartActivity("handläggning");
        activity?.SetTag("diarienummer", diarienummer);

        Arende final;
        try
        {
            var resultat = await DetermineBeslutAsync(arende, _clock.GetUtcNow(), ct);
            final = await FinaliseAsync(arende, resultat);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Finaliseringen (t.ex. en diariet-skrivning) sket sig. Notifiera ändå med en bordläggning
            // så den väntande sidan inte hänger på "Under handläggning" för evigt.
            _logger.LogError(ex, "Finalisering misslyckades för {Diarienummer}; notifierar med bordläggning.", diarienummer);
            final = arende with { Status = Arendestatus.Bordlagt, Beslut = Beslut.BordlaggHandlaggningsfel(_clock.GetUtcNow()) };
            try
            {
                await _diariet.AppendAsync(final);
            }
            catch (Exception persistEx)
            {
                _logger.LogError(persistEx, "Kunde inte skriva bordläggning till diariet för {Diarienummer}.", diarienummer);
            }
        }

        activity?.SetTag("slutstatus", final.Status.ToString());
        activity?.AddEvent(new ActivityEvent("Handläggning avslutad"));

        await _notifier.NotifyAsync(diarienummer, final);
    }

    private async Task<Handlaggningsresultat> DetermineBeslutAsync(Arende arende, DateTimeOffset now, CancellationToken ct)
    {
        if (ArFikahelgd(now))
        {
            return new Handlaggningsresultat(new Avslag(
                "Lampverket befinner sig för tillfället på obligatorisk fikahelgd.",
                "Ansökan inkom under fikahelgd (fredagar kl. 14:00-15:00). Myndigheten är stängd under denna tid.",
                ["Förordningen (2026:42) om fikahelgd"],
                "Detta beslut kan överklagas till Hemautomationsöverdomstolen inom tre veckor.",
                now), null);
        }

        // Synkron IProgress: Progress<T> skulle marshalla callbacken till trådpoolen (ingen
        // SynchronizationContext här), vilket gör ordningen mellan steg/beslut icke-deterministisk.
        var progress = new DirektProgress<Handlaggningshandelse>(handelse => Rapportera(arende, handelse));

        try
        {
            return await _agent.HandlaggaAsync(arende, progress, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Handläggningsfel för {Diarienummer}.", arende.Diarienummer);
            return new Handlaggningsresultat(Beslut.BordlaggHandlaggningsfel(now), null);
        }
    }

    // Mellanliggande uppdateringar från agentloopen → sidan, utan att röra diariet (audit-loggen skrivs
    // först i FinaliseAsync). Fire-and-forget: notifieringen driver bara sidans render över dess circuit.
    private void Rapportera(Arende arende, Handlaggningshandelse handelse)
    {
        if (handelse.Beslut is { } beslut)
        {
            Activity.Current?.AddEvent(new ActivityEvent("Beslut fattat",
                tags: new ActivityTagsCollection { { "beslutstyp", beslut.GetType().Name } }));
            var interim = arende with { Status = beslut.ResulterandeStatus, Beslut = beslut };
            _ = _notifier.NotifyAsync(arende.Diarienummer, interim);
        }
        else if (handelse.Steg is { } steg)
        {
            Activity.Current?.AddEvent(new ActivityEvent(steg));
            _ = _notifier.NotifyStegAsync(arende.Diarienummer, steg);
        }
    }

    private sealed class DirektProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }


    public Task<Arende?> HamtaArendeAsync(string diarienummer) =>
        _diariet.HamtaAsync(diarienummer);

    // Tvåfas: skriv först beslutsfasen (Beslutat/Bordlagt), sedan — om beslutet medger verkställighet
    // — verkställighetsfasen, vars status stäms av mot det observerade utfallet. Diariet får båda
    // stegen som separata poster; audit-loggen berättar sanningen om vad som faktiskt hände.
    private async Task<Arende> FinaliseAsync(Arende arende, Handlaggningsresultat resultat)
    {
        var beslut = resultat.Beslut;

        var beslutat = arende with { Status = beslut.ResulterandeStatus, Beslut = beslut };
        await _diariet.AppendAsync(beslutat);

        if (!beslut.TillaterVerkstallighet)
        {
            return beslutat;
        }

        var (status, utfall) = Verkstallighetsregler.Avgor(beslut, resultat.Verkstallighetsutfall);
        var verkstalld = beslutat with { Status = status, Verkstallighetsutfall = utfall };
        await _diariet.AppendAsync(verkstalld);

        return verkstalld;
    }

    private static bool ArFikahelgd(DateTimeOffset utcNow)
    {
        var stockholm = TimeZoneInfo.ConvertTime(utcNow, _stockholmTz);
        var tid = TimeOnly.FromDateTime(stockholm.DateTime);
        return stockholm.DayOfWeek == DayOfWeek.Friday
            && tid >= FikaStart && tid < FikaSlut;
    }

}
