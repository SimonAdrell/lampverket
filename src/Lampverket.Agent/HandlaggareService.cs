namespace Lampverket.Agent;

using Lampverket.Core;
using Lampverket.HomeAssistant;
using Lampverket.HomeAssistant.Models;

public sealed class HandlaggareService : IHandlaggareService
{
    private static readonly TimeZoneInfo _stockholmTz =
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");

    private readonly IDiariet _diariet;
    private readonly IHomeAssistantClient _ha;
    private readonly IClaudeClient _claude;
    private readonly TimeProvider _clock;
    private int _counter;

    public HandlaggareService(IDiariet diariet, IHomeAssistantClient ha, IClaudeClient claude, TimeProvider clock)
    {
        _diariet = diariet;
        _ha = ha;
        _claude = claude;
        _clock = clock;
    }

    public async Task<Arende> RegisterAnsokanAsync(Ansokan ansokan)
    {
        var now = _clock.GetUtcNow();
        var nr = Interlocked.Increment(ref _counter);
        var diarienummer = $"LV-{now.Year}-{nr:D6}";

        var arende = new Arende
        {
            Diarienummer = diarienummer,
            Mottaget = now,
            Ansokan = ansokan,
            Status = Arendestatus.Inkommet
        };

        await _diariet.AppendAsync(arende);

        Beslut beslut;

        if (ArFikahelgd(now))
        {
            beslut = BygAvslag(now,
                "Lampverket befinner sig för tillfället på obligatorisk fikahelgd.",
                "Ansökan inkom under fikahelgd (fredagar kl. 14:00–15:00). Myndigheten är stängd under denna tid.",
                ["Förordningen (2026:42) om fikahelgd"],
                "Detta beslut kan överklagas till Hemautomationsöverdomstolen inom tre veckor.");
        }
        else
        {
            DeviceState state;
            try
            {
                state = await _ha.GetStateAsync(ansokan.BerordEnhet);
            }
            catch (ArgumentException)
            {
                beslut = BygAvslag(now,
                    "Lampverket kan inte verkställa ärendet — enheten är okänd.",
                    "Den angivna enheten finns inte i Lampverkets enhetsregister. Ärendet bordläggs.",
                    [],
                    "Kontakta Lampverket för rättelse av enhetsuppgifter.");
                return await FinaliseAsync(arende, beslut);
            }

            if (!state.IsAvailable)
            {
                beslut = BygBordlaggning(now, ansokan.BerordEnhet);
            }
            else if (ArObehövligtArende(ansokan, state))
            {
                beslut = BygAvslag(now,
                    "Lampverket avslår ansökan.",
                    $"Enheten befinner sig redan i det önskade tillståndet. Åtgärden är obehövlig.",
                    ["7 § lagen (2026:1) om skälig hemtrevnad"],
                    "Beslutet kan överklagas till Hemautomationsöverdomstolen inom tre veckor.");
            }
            else
            {
                beslut = await _claude.BegarBeslutAsync(arende, FormatState(state)) ??
                    BygBordlaggning(now, ansokan.BerordEnhet, "Handläggaragentens svar var ogiltigt.");

                if (beslut.Beslutstyp is Beslutstyp.Bifall or Beslutstyp.DelvisBifall)
                {
                    var haResult = await VerkstallAsync(ansokan, beslut);
                    if (haResult is HaResult.DeviceUnavailable or HaResult.ToolError)
                        beslut = BygBordlaggning(now, ansokan.BerordEnhet);
                }
            }
        }

        return await FinaliseAsync(arende, beslut);
    }

    public Task<Arende?> HamtaArendeAsync(string diarienummer) =>
        _diariet.HamtaAsync(diarienummer);

    private async Task<Arende> FinaliseAsync(Arende arende, Beslut beslut)
    {
        var status = beslut.Beslutstyp is Beslutstyp.Bifall or Beslutstyp.DelvisBifall
            ? Arendestatus.Verkstallt
            : Arendestatus.Beslutat;

        var final = arende with { Status = status, Beslut = beslut };
        await _diariet.AppendAsync(final);
        return final;
    }

    private async Task<HaResult> VerkstallAsync(Ansokan ansokan, Beslut beslut)
    {
        _ = beslut; // beslut recorded; HA call is the side effect
        return ansokan.Arendetyp switch
        {
            Arendetyp.Tandning => await _ha.TurnOnAsync(ansokan.BerordEnhet),
            Arendetyp.Slackning => await _ha.TurnOffAsync(ansokan.BerordEnhet),
            Arendetyp.Ljusstyrka when int.TryParse(ansokan.OnskadAtgard, out var pct)
                => await _ha.SetBrightnessAsync(ansokan.BerordEnhet, pct),
            Arendetyp.Volym when int.TryParse(ansokan.OnskadAtgard, out var pct)
                => await _ha.SetVolumeAsync(ansokan.BerordEnhet, pct),
            Arendetyp.Media when ansokan.OnskadAtgard is not null
                => await _ha.PlayMediaAsync(ansokan.BerordEnhet, ansokan.OnskadAtgard),
            _ => new HaResult.Ok()
        };
    }

    private bool ArFikahelgd(DateTimeOffset utcNow)
    {
        var stockholm = TimeZoneInfo.ConvertTime(utcNow, _stockholmTz);
        return stockholm.DayOfWeek == DayOfWeek.Friday && stockholm.Hour == 14;
    }

    private static bool ArObehövligtArende(Ansokan ansokan, DeviceState state) =>
        ansokan.Arendetyp switch
        {
            Arendetyp.Tandning => state.IsOn,
            Arendetyp.Slackning => !state.IsOn,
            _ => false
        };

    private static Beslut BygAvslag(DateTimeOffset now, string beslutstext, string motivering,
        string[] lagrum, string overklagandehanvisning) => new()
    {
        Beslutstyp = Beslutstyp.Avslag,
        Beslutstext = beslutstext,
        Motivering = motivering,
        Lagrum = lagrum,
        Overklagandehanvisning = overklagandehanvisning,
        Verkstallighet = "Ingen åtgärd vidtagen.",
        Datum = now
    };

    private static Beslut BygBordlaggning(DateTimeOffset now, string enhet, string? extra = null) => new()
    {
        Beslutstyp = Beslutstyp.Avslag,
        Beslutstext = "Lampverket bordlägger ärendet.",
        Motivering = extra ?? $"Enheten \"{enhet}\" är ur funktion; ärendet bordläggs tills vidare.",
        Lagrum = [],
        Overklagandehanvisning = "Beslutet kan överklagas till Hemautomationsöverdomstolen inom tre veckor.",
        Verkstallighet = "Ingen åtgärd vidtagen.",
        Datum = now
    };

    private static string FormatState(DeviceState state) =>
        $"Enhet: {state.FriendlyName} | Tillstånd: {(state.IsOn ? "på" : "av")}" +
        (state.BrightnessPercent.HasValue ? $" | Ljusstyrka: {state.BrightnessPercent}%" : "");
}
