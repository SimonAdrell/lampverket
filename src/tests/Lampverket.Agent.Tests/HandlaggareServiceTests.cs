using System.Threading.Channels;
using Lampverket.Core;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lampverket.Agent.Tests;

public class HandlaggareServiceTests
{
    [Fact]
    public async Task RegisterAnsokanAsync_ReturnsDiarienummerInCorrectFormat()
    {
        var now = new DateTimeOffset(2026, 5, 15, 10, 0, 0, TimeSpan.Zero);
        var sut = MakeSut(new FakeTimeProvider(now));

        var arende = await sut.RegisterAnsokanAsync(TestAnsokan("Banan", Arendetyp.Tanding));

        Assert.Matches(@"^LV-2026-\d{6}$", arende.Diarienummer);
    }

    [Fact]
    public async Task RegisterAnsokanAsync_AppendsInkommetToDiariet()
    {
        var diariet = new FakeDiariet();
        var now = new DateTimeOffset(2026, 5, 15, 10, 0, 0, TimeSpan.Zero);
        var sut = MakeSut(new FakeTimeProvider(now), diariet: diariet);

        await sut.RegisterAnsokanAsync(TestAnsokan("Banan", Arendetyp.Tanding));

        var entry = Assert.Single(diariet.Log);
        Assert.Equal(Arendestatus.Inkommet, entry.Status);
    }

    [Fact]
    public async Task RegisterAnsokanAsync_DoesNotInvokeAgent()
    {
        var fakeAgent = new FakeHandlaggareAgent();
        var now = new DateTimeOffset(2026, 5, 15, 10, 0, 0, TimeSpan.Zero);
        var sut = MakeSut(new FakeTimeProvider(now), agent: fakeAgent);

        await sut.RegisterAnsokanAsync(TestAnsokan("Banan", Arendetyp.Tanding));

        Assert.Equal(0, fakeAgent.CallCount);
    }

    [Fact]
    public async Task HamtaArendeAsync_ReturnsPreviouslyRegisteredArende()
    {
        var diariet = new FakeDiariet();
        var now = new DateTimeOffset(2026, 5, 15, 10, 0, 0, TimeSpan.Zero);
        var sut = MakeSut(new FakeTimeProvider(now), diariet: diariet);

        var arende = await sut.RegisterAnsokanAsync(TestAnsokan("Banan", Arendetyp.Tanding));
        var hamtad = await sut.HamtaArendeAsync(arende.Diarienummer);

        Assert.NotNull(hamtad);
        Assert.Equal(arende.Diarienummer, hamtad.Diarienummer);
    }

    [Fact]
    public async Task ProcessArendeAsync_Fikahelgd_ReturnsAvslag_AgentNotCalled()
    {
        // Friday 14:30 Stockholm CEST (UTC+2) = 12:30 UTC
        var friday1430Stockholm = new DateTimeOffset(2026, 5, 22, 12, 30, 0, TimeSpan.Zero);
        var fakeAgent = new FakeHandlaggareAgent();
        var diariet = new FakeDiariet();
        var sut = MakeSut(new FakeTimeProvider(friday1430Stockholm), diariet: diariet, agent: fakeAgent);

        var arende = await sut.RegisterAnsokanAsync(TestAnsokan("Banan", Arendetyp.Tanding));
        await sut.ProcessArendeAsync(arende.Diarienummer);
        var final = await sut.HamtaArendeAsync(arende.Diarienummer);

        Assert.IsType<Avslag>(final!.Beslut);
        Assert.Equal(0, fakeAgent.CallCount);
    }

    [Fact]
    public async Task ProcessArendeAsync_Thursday14h_AgentIsCalled()
    {
        // Thursday 14:30 Stockholm CEST = 12:30 UTC — NOT fikahelgd
        var thursday1430Stockholm = new DateTimeOffset(2026, 5, 21, 12, 30, 0, TimeSpan.Zero);
        var fakeAgent = new FakeHandlaggareAgent();
        fakeAgent.Returns(TestBifall());
        var diariet = new FakeDiariet();
        var sut = MakeSut(new FakeTimeProvider(thursday1430Stockholm), diariet: diariet, agent: fakeAgent);

        var arende = await sut.RegisterAnsokanAsync(TestAnsokan("Banan", Arendetyp.Tanding));
        await sut.ProcessArendeAsync(arende.Diarienummer);

        Assert.Equal(1, fakeAgent.CallCount);
    }

    [Fact]
    public async Task ProcessArendeAsync_AgentReturnsBifall_StatusIsVerkstallt()
    {
        var now = new DateTimeOffset(2026, 5, 15, 10, 0, 0, TimeSpan.Zero);
        var fakeAgent = new FakeHandlaggareAgent();
        fakeAgent.Returns(TestBifall(), Verkstallighetsstatus.Verkstalld);
        var diariet = new FakeDiariet();
        var sut = MakeSut(new FakeTimeProvider(now), diariet: diariet, agent: fakeAgent);

        var arende = await sut.RegisterAnsokanAsync(TestAnsokan("Banan", Arendetyp.Tanding));
        await sut.ProcessArendeAsync(arende.Diarienummer);
        var final = await sut.HamtaArendeAsync(arende.Diarienummer);

        Assert.IsType<Bifall>(final!.Beslut);
        Assert.Equal(Arendestatus.Verkstallt, final.Status);
    }

    [Fact]
    public async Task ProcessArendeAsync_AgentReturnsAvslag_StatusIsBeslutat()
    {
        var now = new DateTimeOffset(2026, 5, 15, 10, 0, 0, TimeSpan.Zero);
        var fakeAgent = new FakeHandlaggareAgent();
        fakeAgent.Returns(TestAvslag());
        var diariet = new FakeDiariet();
        var sut = MakeSut(new FakeTimeProvider(now), diariet: diariet, agent: fakeAgent);

        var arende = await sut.RegisterAnsokanAsync(TestAnsokan("Banan", Arendetyp.Tanding));
        await sut.ProcessArendeAsync(arende.Diarienummer);
        var final = await sut.HamtaArendeAsync(arende.Diarienummer);

        Assert.IsType<Avslag>(final!.Beslut);
        Assert.Equal(Arendestatus.Beslutat, final.Status);
    }

    [Fact]
    public async Task ProcessArendeAsync_AgentReturnsBordlaggning_StatusIsBordlagt()
    {
        var now = new DateTimeOffset(2026, 5, 15, 10, 0, 0, TimeSpan.Zero);
        var fakeAgent = new FakeHandlaggareAgent();
        fakeAgent.Returns(Beslut.BordlaggUtanBeslut(now));
        var diariet = new FakeDiariet();
        var sut = MakeSut(new FakeTimeProvider(now), diariet: diariet, agent: fakeAgent);

        var arende = await sut.RegisterAnsokanAsync(TestAnsokan("Banan", Arendetyp.Tanding));
        await sut.ProcessArendeAsync(arende.Diarienummer);
        var final = await sut.HamtaArendeAsync(arende.Diarienummer);

        Assert.IsType<Bordlaggning>(final!.Beslut);
        Assert.Equal(Arendestatus.Bordlagt, final.Status);
    }

    [Fact]
    public async Task ProcessArendeAsync_AgentThrows_FallsBackToBordlaggning()
    {
        var now = new DateTimeOffset(2026, 5, 15, 10, 0, 0, TimeSpan.Zero);
        var diariet = new FakeDiariet();
        var sut = MakeSut(new FakeTimeProvider(now), diariet: diariet,
            agent: new ThrowingHandlaggareAgent(new InvalidOperationException("Simulated agent failure.")));

        var arende = await sut.RegisterAnsokanAsync(TestAnsokan("Banan", Arendetyp.Tanding));
        await sut.ProcessArendeAsync(arende.Diarienummer);
        var final = await sut.HamtaArendeAsync(arende.Diarienummer);

        Assert.IsType<Bordlaggning>(final!.Beslut);
        Assert.Equal(Arendestatus.Bordlagt, final.Status);
    }

    [Fact]
    public async Task ProcessArendeAsync_NotifiesAfterFinalise()
    {
        var notifier = new FakeArendeNotifier();
        var now = new DateTimeOffset(2026, 5, 15, 10, 0, 0, TimeSpan.Zero);
        var fakeAgent = new FakeHandlaggareAgent();
        fakeAgent.Returns(TestBifall(), Verkstallighetsstatus.Verkstalld);
        var diariet = new FakeDiariet();
        var sut = MakeSut(new FakeTimeProvider(now), diariet: diariet, agent: fakeAgent, notifier: notifier);

        var arende = await sut.RegisterAnsokanAsync(TestAnsokan("Banan", Arendetyp.Tanding));
        await sut.ProcessArendeAsync(arende.Diarienummer);

        Assert.Single(notifier.Calls);
        Assert.Equal(arende.Diarienummer, notifier.Calls[0].Diarienummer);
        Assert.Equal(Arendestatus.Verkstallt, notifier.Calls[0].Arende.Status);
    }

    [Fact]
    public async Task ProcessArendeAsync_BifallButExecutionFailed_StatusStaysBeslutat()
    {
        var now = new DateTimeOffset(2026, 5, 15, 10, 0, 0, TimeSpan.Zero);
        var fakeAgent = new FakeHandlaggareAgent();
        fakeAgent.Returns(TestBifall(), Verkstallighetsstatus.Misslyckad);
        var diariet = new FakeDiariet();
        var sut = MakeSut(new FakeTimeProvider(now), diariet: diariet, agent: fakeAgent);

        var arende = await sut.RegisterAnsokanAsync(TestAnsokan("Banan", Arendetyp.Tanding));
        await sut.ProcessArendeAsync(arende.Diarienummer);
        var final = await sut.HamtaArendeAsync(arende.Diarienummer);

        // Beslutet står kvar (motiveringen bevaras), men ärendet når aldrig Verkställt.
        Assert.IsType<Bifall>(final!.Beslut);
        Assert.Equal(Arendestatus.Beslutat, final.Status);
        Assert.Equal(Verkstallighetsstatus.Misslyckad, final.Verkstallighetsutfall);
    }

    [Fact]
    public async Task ProcessArendeAsync_BifallWithNoAction_StaysBeslutat()
    {
        var now = new DateTimeOffset(2026, 5, 15, 10, 0, 0, TimeSpan.Zero);
        var fakeAgent = new FakeHandlaggareAgent();
        fakeAgent.Returns(TestBifall(), utfall: null); // beslut men inget verkställande anrop
        var diariet = new FakeDiariet();
        var sut = MakeSut(new FakeTimeProvider(now), diariet: diariet, agent: fakeAgent);

        var arende = await sut.RegisterAnsokanAsync(TestAnsokan("Banan", Arendetyp.Tanding));
        await sut.ProcessArendeAsync(arende.Diarienummer);
        var final = await sut.HamtaArendeAsync(arende.Diarienummer);

        // Ingen bekräftad åtgärd → ärendet får inte påstås vara Verkställt.
        Assert.Equal(Arendestatus.Beslutat, final!.Status);
        Assert.Null(final.Verkstallighetsutfall);
    }

    [Fact]
    public async Task ProcessArendeAsync_Bifall_WritesTwoPhaseDiariet()
    {
        var now = new DateTimeOffset(2026, 5, 15, 10, 0, 0, TimeSpan.Zero);
        var fakeAgent = new FakeHandlaggareAgent();
        fakeAgent.Returns(TestBifall(), Verkstallighetsstatus.Verkstalld);
        var diariet = new FakeDiariet();
        var sut = MakeSut(new FakeTimeProvider(now), diariet: diariet, agent: fakeAgent);

        var arende = await sut.RegisterAnsokanAsync(TestAnsokan("Banan", Arendetyp.Tanding));
        await sut.ProcessArendeAsync(arende.Diarienummer);

        Assert.Equal(
            [Arendestatus.Inkommet, Arendestatus.Beslutat, Arendestatus.Verkstallt],
            diariet.Log.Select(a => a.Status));
    }

    [Fact]
    public async Task ProcessArendeAsync_Avslag_WritesOnlyBeslutsfas()
    {
        var now = new DateTimeOffset(2026, 5, 15, 10, 0, 0, TimeSpan.Zero);
        var fakeAgent = new FakeHandlaggareAgent();
        fakeAgent.Returns(TestAvslag());
        var diariet = new FakeDiariet();
        var sut = MakeSut(new FakeTimeProvider(now), diariet: diariet, agent: fakeAgent);

        var arende = await sut.RegisterAnsokanAsync(TestAnsokan("Banan", Arendetyp.Tanding));
        await sut.ProcessArendeAsync(arende.Diarienummer);

        // Ingen verkställighetsfas för icke-verkställbara beslut.
        Assert.Equal(
            [Arendestatus.Inkommet, Arendestatus.Beslutat],
            diariet.Log.Select(a => a.Status));
    }

    [Fact]
    public async Task ProcessArendeAsync_FinaliseThrows_StillNotifiesWithBordlaggning()
    {
        var notifier = new FakeArendeNotifier();
        var now = new DateTimeOffset(2026, 5, 15, 10, 0, 0, TimeSpan.Zero);
        var fakeAgent = new FakeHandlaggareAgent();
        fakeAgent.Returns(TestBifall(), Verkstallighetsstatus.Verkstalld);
        var diariet = new ThrowingOnAppendDiariet(
            new Arende("LV-2026-000001", now, TestAnsokan("Banan", Arendetyp.Tanding), Arendestatus.Inkommet));
        var sut = MakeSut(new FakeTimeProvider(now), diariet: diariet, agent: fakeAgent, notifier: notifier);

        await sut.ProcessArendeAsync("LV-2026-000001");

        // Sidan får ett svar (bordläggning) i stället för att hänga för evigt.
        Assert.Single(notifier.Calls);
        Assert.Equal(Arendestatus.Bordlagt, notifier.Calls[0].Arende.Status);
        Assert.IsType<Bordlaggning>(notifier.Calls[0].Arende.Beslut);
    }

    [Fact]
    public async Task ProcessArendeAsync_AgentReportsBeslutMidLoop_NotifiesInterimWithoutExtraDiarietRow()
    {
        var notifier = new FakeArendeNotifier();
        var now = new DateTimeOffset(2026, 5, 15, 10, 0, 0, TimeSpan.Zero);
        var beslut = TestBifall();
        // Agenten rapporterar ett steg och sedan det fattade beslutet mitt i loopen, före verkställighet.
        var fakeAgent = new ReportingHandlaggareAgent(beslut, Verkstallighetsstatus.Verkstalld,
            Handlaggningshandelse.ForSteg("Granskar hemförhållanden"),
            Handlaggningshandelse.ForBeslut(beslut));
        var diariet = new FakeDiariet();
        var sut = MakeSut(new FakeTimeProvider(now), diariet: diariet, agent: fakeAgent, notifier: notifier);

        var arende = await sut.RegisterAnsokanAsync(TestAnsokan("Banan", Arendetyp.Tanding));
        await sut.ProcessArendeAsync(arende.Diarienummer);

        // Steget nådde sidan.
        Assert.Contains("Granskar hemförhållanden", notifier.Steg);

        // Beslutet visades direkt (interim, Beslutat) och sedan slutstatus (Verkställt) — två notiser.
        Assert.Equal(2, notifier.Calls.Count);
        Assert.Equal(Arendestatus.Beslutat, notifier.Calls[0].Arende.Status);
        Assert.Same(beslut, notifier.Calls[0].Arende.Beslut);
        Assert.Equal(Arendestatus.Verkstallt, notifier.Calls[1].Arende.Status);

        // Interim-notisen får inte skriva till diariet: audit-loggen förblir Inkommet → Beslutat → Verkställt.
        Assert.Equal(
            [Arendestatus.Inkommet, Arendestatus.Beslutat, Arendestatus.Verkstallt],
            diariet.Log.Select(a => a.Status));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static HandlaggareService MakeSut(
        TimeProvider? clock = null,
        IDiariet? diariet = null,
        IHandlaggareAgent? agent = null,
        IArendeNotifier? notifier = null,
        ChannelWriter<string>? queue = null) =>
        new(diariet ?? new FakeDiariet(),
            agent ?? new FakeHandlaggareAgent(),
            clock ?? TimeProvider.System,
            notifier ?? new NullArendeNotifier(),
            queue ?? Channel.CreateUnbounded<string>().Writer,
            NullLogger<HandlaggareService>.Instance);

    private static Ansokan TestAnsokan(string device, Arendetyp typ) => new()
    {
        Personnummer = "19900101-1234",
        Sokande = "Test Testsson",
        Arendetyp = typ,
        BerordEnhet = new Enhet(device, device),
        Motivering = "Testmotivering.",
        OnskatDatum = DateOnly.FromDateTime(DateTime.Today)
    };

    private static Bifall TestBifall() => new(
        "Testbeslut.",
        "Testmotivering.",
        ["7 § lagen (2026:1) om skälig hemtrevnad"],
        "Kan överklagas.",
        "Lampan tändes.",
        DateTimeOffset.UtcNow);

    private static Avslag TestAvslag() => new(
        "Testbeslut.",
        "Testmotivering.",
        ["7 § lagen (2026:1) om skälig hemtrevnad"],
        "Kan överklagas.",
        DateTimeOffset.UtcNow);
}

file sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow.ToUniversalTime();
}

file sealed class FakeDiariet : IDiariet
{
    private readonly List<Arende> _log = [];
    private int _counter;
    public IReadOnlyList<Arende> Log => _log.AsReadOnly();

    public Task AppendAsync(Arende arende) { _log.Add(arende); return Task.CompletedTask; }
    public Task<IReadOnlyList<Arende>> HamtaAllaAsync() =>
        Task.FromResult<IReadOnlyList<Arende>>(_log.AsReadOnly());
    public Task<Arende?> HamtaAsync(string diarienummer) =>
        Task.FromResult(_log.LastOrDefault(a => a.Diarienummer == diarienummer));
    public Task<string> AllokeraDiarienummerAsync(int year) =>
        Task.FromResult($"LV-{year}-{Interlocked.Increment(ref _counter):D6}");
}

file sealed class ThrowingOnAppendDiariet(Arende arende) : IDiariet
{
    private readonly Arende _arende = arende;
    public Task AppendAsync(Arende a) => throw new InvalidOperationException("Diariet nere.");
    public Task<IReadOnlyList<Arende>> HamtaAllaAsync() => Task.FromResult<IReadOnlyList<Arende>>([_arende]);
    public Task<Arende?> HamtaAsync(string diarienummer) =>
        Task.FromResult<Arende?>(diarienummer == _arende.Diarienummer ? _arende : null);
    public Task<string> AllokeraDiarienummerAsync(int year) => Task.FromResult(_arende.Diarienummer);
}

file sealed class FakeHandlaggareAgent : IHandlaggareAgent
{
    private Beslut? _beslut;
    private Verkstallighetsstatus? _utfall;
    public int CallCount { get; private set; }
    public void Returns(Beslut b, Verkstallighetsstatus? utfall = null)
    {
        _beslut = b;
        _utfall = utfall;
    }
    public Task<Handlaggningsresultat> HandlaggaAsync(
        Arende arende, IProgress<Handlaggningshandelse>? progress = null, CancellationToken ct = default)
    {
        CallCount++;
        return Task.FromResult(new Handlaggningsresultat(_beslut!, _utfall));
    }
}

file sealed class ThrowingHandlaggareAgent(Exception ex) : IHandlaggareAgent
{
    public Task<Handlaggningsresultat> HandlaggaAsync(
        Arende arende, IProgress<Handlaggningshandelse>? progress = null, CancellationToken ct = default) => throw ex;
}

// Rapporterar givna händelser via IProgress (som den riktiga agenten gör mitt i loopen) innan resultatet returneras.
file sealed class ReportingHandlaggareAgent(
    Beslut beslut, Verkstallighetsstatus? utfall, params Handlaggningshandelse[] handelser) : IHandlaggareAgent
{
    public Task<Handlaggningsresultat> HandlaggaAsync(
        Arende arende, IProgress<Handlaggningshandelse>? progress = null, CancellationToken ct = default)
    {
        foreach (var handelse in handelser)
        {
            progress?.Report(handelse);
        }
        return Task.FromResult(new Handlaggningsresultat(beslut, utfall));
    }
}

file sealed class NullArendeNotifier : IArendeNotifier
{
    public Task NotifyAsync(string diarienummer, Arende arende) => Task.CompletedTask;
    public IDisposable Subscribe(string diarienummer, Func<Arende, Task> handler) => new NoopDisposable();
    public Task NotifyStegAsync(string diarienummer, string steg) => Task.CompletedTask;
    public IDisposable SubscribeSteg(string diarienummer, Func<string, Task> handler) => new NoopDisposable();

    private sealed class NoopDisposable : IDisposable { public void Dispose() { } }
}

file sealed class FakeArendeNotifier : IArendeNotifier
{
    public record Call(string Diarienummer, Arende Arende);
    public List<Call> Calls { get; } = [];
    public List<string> Steg { get; } = [];
    public Task NotifyAsync(string diarienummer, Arende arende)
    {
        Calls.Add(new(diarienummer, arende));
        return Task.CompletedTask;
    }
    public IDisposable Subscribe(string diarienummer, Func<Arende, Task> handler) => new NoopDisposable();
    public Task NotifyStegAsync(string diarienummer, string steg)
    {
        Steg.Add(steg);
        return Task.CompletedTask;
    }
    public IDisposable SubscribeSteg(string diarienummer, Func<string, Task> handler) => new NoopDisposable();

    private sealed class NoopDisposable : IDisposable { public void Dispose() { } }
}
