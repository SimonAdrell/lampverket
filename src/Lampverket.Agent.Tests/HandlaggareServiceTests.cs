using Lampverket.Agent;
using Lampverket.Core;
using Lampverket.HomeAssistant;
using Lampverket.HomeAssistant.Models;

namespace Lampverket.Agent.Tests;

public class HandlaggareServiceTests
{
    // ── Step 1: project compiles ──────────────────────────────────────────────
    [Fact]
    public void ProjectCompiles()
    {
        Assert.True(true);
    }

    // ── Step 2: diarienummer format LV-YYYY-NNNNNN ───────────────────────────
    [Fact]
    public async Task RegisterAnsokanAsync_ReturnsDiarienummerInCorrectFormat()
    {
        var now = new DateTimeOffset(2026, 5, 15, 10, 0, 0, TimeSpan.Zero); // Thursday 10:00 Stockholm
        var sut = MakeSut(new FakeTimeProvider(now));

        var arende = await sut.RegisterAnsokanAsync(TestAnsokan("Banan", Arendetyp.Tandning));

        Assert.Matches(@"^LV-2026-\d{6}$", arende.Diarienummer);
    }

    // ── Step 3: AppendAsync called (C3a fix) ─────────────────────────────────
    [Fact]
    public async Task RegisterAnsokanAsync_CallsAppendAsync()
    {
        var diariet = new FakeDiariet();
        var now = new DateTimeOffset(2026, 5, 15, 10, 0, 0, TimeSpan.Zero);
        var sut = MakeSut(new FakeTimeProvider(now), diariet: diariet);

        await sut.RegisterAnsokanAsync(TestAnsokan("Banan", Arendetyp.Tandning));

        Assert.NotEmpty(diariet.Log);
    }

    // ── Step 4: HamtaArendeAsync delegates to diariet (C3b fix) ──────────────
    [Fact]
    public async Task HamtaArendeAsync_ReturnsPreviouslyRegisteredArende()
    {
        var diariet = new FakeDiariet();
        var now = new DateTimeOffset(2026, 5, 15, 10, 0, 0, TimeSpan.Zero);
        var sut = MakeSut(new FakeTimeProvider(now), diariet: diariet);

        var arende = await sut.RegisterAnsokanAsync(TestAnsokan("Banan", Arendetyp.Tandning));
        var hamtad = await sut.HamtaArendeAsync(arende.Diarienummer);

        Assert.NotNull(hamtad);
        Assert.Equal(arende.Diarienummer, hamtad.Diarienummer);
    }

    // ── Step 5: Fikahelgd — Friday 14:xx Stockholm → auto-Avslag ─────────────
    [Fact]
    public async Task RegisterAnsokanAsync_FrikahelgdFriday14h_ReturnsAvslag_ClaudeNotCalled()
    {
        // Friday 14:30 Stockholm CEST (UTC+2) = 12:30 UTC
        var friday1430Stockholm = new DateTimeOffset(2026, 5, 22, 12, 30, 0, TimeSpan.Zero);
        var fakeClaude = new FakeClaudeClient();
        var sut = MakeSut(new FakeTimeProvider(friday1430Stockholm), claude: fakeClaude);

        var arende = await sut.RegisterAnsokanAsync(TestAnsokan("Banan", Arendetyp.Tandning));

        Assert.Equal(Beslutstyp.Avslag, arende.Beslut!.Beslutstyp);
        Assert.Equal(0, fakeClaude.CallCount);
    }

    [Fact]
    public async Task RegisterAnsokanAsync_Thursday14h_ClaudeIsCalled()
    {
        // Thursday 14:30 Stockholm CEST = 12:30 UTC — NOT fikahelgd
        var thursday1430Stockholm = new DateTimeOffset(2026, 5, 21, 12, 30, 0, TimeSpan.Zero);
        var fakeClaude = new FakeClaudeClient();
        fakeClaude.Returns(TestBeslut(Beslutstyp.Bifall));
        var sut = MakeSut(new FakeTimeProvider(thursday1430Stockholm), claude: fakeClaude);

        await sut.RegisterAnsokanAsync(TestAnsokan("Banan", Arendetyp.Tandning));

        Assert.Equal(1, fakeClaude.CallCount);
    }

    // ── Step 6: Already-on → Avslag, Claude not called ───────────────────────
    [Fact]
    public async Task RegisterAnsokanAsync_DeviceAlreadyOn_Tandning_ReturnsAvslag_ClaudeNotCalled()
    {
        var now = new DateTimeOffset(2026, 5, 15, 10, 0, 0, TimeSpan.Zero);
        var fakeHa = new FakeHomeAssistantClient();
        fakeHa.DeviceIsOn("Banan");
        var fakeClaude = new FakeClaudeClient();
        var sut = MakeSut(new FakeTimeProvider(now), ha: fakeHa, claude: fakeClaude);

        var arende = await sut.RegisterAnsokanAsync(TestAnsokan("Banan", Arendetyp.Tandning));

        Assert.Equal(Beslutstyp.Avslag, arende.Beslut!.Beslutstyp);
        Assert.Equal(0, fakeClaude.CallCount);
    }

    [Fact]
    public async Task RegisterAnsokanAsync_DeviceAlreadyOff_Slackning_ReturnsAvslag_ClaudeNotCalled()
    {
        var now = new DateTimeOffset(2026, 5, 15, 10, 0, 0, TimeSpan.Zero);
        var fakeHa = new FakeHomeAssistantClient();
        fakeHa.DeviceIsOff("Banan");
        var fakeClaude = new FakeClaudeClient();
        var sut = MakeSut(new FakeTimeProvider(now), ha: fakeHa, claude: fakeClaude);

        var arende = await sut.RegisterAnsokanAsync(TestAnsokan("Banan", Arendetyp.Slackning));

        Assert.Equal(Beslutstyp.Avslag, arende.Beslut!.Beslutstyp);
        Assert.Equal(0, fakeClaude.CallCount);
    }

    // ── Step 7: FakeClaudeClient returns Bifall → Arende.Beslut reflects it ──
    [Fact]
    public async Task RegisterAnsokanAsync_ClaudeReturnsBifall_ArendeBeslutIsBifall()
    {
        var now = new DateTimeOffset(2026, 5, 15, 10, 0, 0, TimeSpan.Zero);
        var fakeHa = new FakeHomeAssistantClient();
        fakeHa.DeviceIsOff("Banan");
        var fakeClaude = new FakeClaudeClient();
        fakeClaude.Returns(TestBeslut(Beslutstyp.Bifall));
        var sut = MakeSut(new FakeTimeProvider(now), ha: fakeHa, claude: fakeClaude);

        var arende = await sut.RegisterAnsokanAsync(TestAnsokan("Banan", Arendetyp.Tandning));

        Assert.Equal(Beslutstyp.Bifall, arende.Beslut!.Beslutstyp);
    }

    // ── Step 8: Bifall + Tandning → HA TurnOnAsync called ────────────────────
    [Fact]
    public async Task RegisterAnsokanAsync_Bifall_Tandning_CallsTurnOn()
    {
        var now = new DateTimeOffset(2026, 5, 15, 10, 0, 0, TimeSpan.Zero);
        var fakeHa = new FakeHomeAssistantClient();
        fakeHa.DeviceIsOff("Banan");
        var fakeClaude = new FakeClaudeClient();
        fakeClaude.Returns(TestBeslut(Beslutstyp.Bifall));
        var sut = MakeSut(new FakeTimeProvider(now), ha: fakeHa, claude: fakeClaude);

        await sut.RegisterAnsokanAsync(TestAnsokan("Banan", Arendetyp.Tandning));

        Assert.Contains("Banan", fakeHa.TurnOnCalls);
    }

    // ── Step 9: Avslag → HA NOT called ───────────────────────────────────────
    [Fact]
    public async Task RegisterAnsokanAsync_Avslag_DoesNotCallHaTurnOn()
    {
        var now = new DateTimeOffset(2026, 5, 15, 10, 0, 0, TimeSpan.Zero);
        var fakeHa = new FakeHomeAssistantClient();
        fakeHa.DeviceIsOff("Banan");
        var fakeClaude = new FakeClaudeClient();
        fakeClaude.Returns(TestBeslut(Beslutstyp.Avslag));
        var sut = MakeSut(new FakeTimeProvider(now), ha: fakeHa, claude: fakeClaude);

        await sut.RegisterAnsokanAsync(TestAnsokan("Banan", Arendetyp.Tandning));

        Assert.Empty(fakeHa.TurnOnCalls);
    }

    // ── Step 10: Unavailable device → bordläggning, no exception ─────────────
    [Fact]
    public async Task RegisterAnsokanAsync_DeviceUnavailable_ReturnsBordlaggning_NoException()
    {
        var now = new DateTimeOffset(2026, 5, 15, 10, 0, 0, TimeSpan.Zero);
        var fakeHa = new FakeHomeAssistantClient();
        fakeHa.DeviceIsUnavailable("Banan");
        var fakeClaude = new FakeClaudeClient();
        var sut = MakeSut(new FakeTimeProvider(now), ha: fakeHa, claude: fakeClaude);

        var arende = await sut.RegisterAnsokanAsync(TestAnsokan("Banan", Arendetyp.Tandning));

        Assert.NotNull(arende.Beslut);
        Assert.Contains("bordlägg", arende.Beslut.Motivering, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, fakeClaude.CallCount);
    }

    [Fact]
    public async Task RegisterAnsokanAsync_DeviceUnavailable_PersistsToDiariet()
    {
        var now = new DateTimeOffset(2026, 5, 15, 10, 0, 0, TimeSpan.Zero);
        var fakeHa = new FakeHomeAssistantClient();
        fakeHa.DeviceIsUnavailable("Banan");
        var diariet = new FakeDiariet();
        var sut = MakeSut(new FakeTimeProvider(now), diariet: diariet, ha: fakeHa);

        await sut.RegisterAnsokanAsync(TestAnsokan("Banan", Arendetyp.Tandning));

        Assert.True(diariet.Log.Count >= 2, "Expected at least Inkommet + final append");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static HandlaggareService MakeSut(
        TimeProvider? clock = null,
        IDiariet? diariet = null,
        IClaudeClient? claude = null,
        IHomeAssistantClient? ha = null,
        string defaultDevice = "Banan")
    {
        var fakeHa = (ha as FakeHomeAssistantClient) ?? new FakeHomeAssistantClient();
        if (!fakeHa.HasDevice(defaultDevice))
            fakeHa.DeviceIsOff(defaultDevice);
        return new HandlaggareService(
            diariet ?? new FakeDiariet(),
            fakeHa,
            claude ?? new FakeClaudeClient(),
            clock ?? TimeProvider.System);
    }

    private static Ansokan TestAnsokan(string device, Arendetyp typ) => new()
    {
        Personnummer = "19900101-1234",
        Sokande = "Test Testsson",
        Arendetyp = typ,
        BerordEnhet = device,
        Motivering = "Testmotivering.",
        OnskatDatum = DateOnly.FromDateTime(DateTime.Today)
    };

    private static Beslut TestBeslut(Beslutstyp typ) => new()
    {
        Beslutstyp = typ,
        Beslutstext = "Testbeslut.",
        Motivering = "Testmotivering.",
        Lagrum = ["7 § lagen (2026:1) om skälig hemtrevnad"],
        Overklagandehanvisning = "Kan överklagas.",
        Verkstallighet = "Lampan tändes.",
        Datum = DateTimeOffset.UtcNow
    };
}

// ── Shared fakes (file-scoped) ────────────────────────────────────────────────

file sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow.ToUniversalTime();
}

file sealed class FakeDiariet : IDiariet
{
    private readonly List<Arende> _log = [];
    public IReadOnlyList<Arende> Log => _log.AsReadOnly();

    public Task AppendAsync(Arende arende) { _log.Add(arende); return Task.CompletedTask; }
    public Task<IReadOnlyList<Arende>> HamtaAllaAsync() =>
        Task.FromResult<IReadOnlyList<Arende>>(_log.AsReadOnly());
    public Task<Arende?> HamtaAsync(string diarienummer) =>
        Task.FromResult(_log.LastOrDefault(a => a.Diarienummer == diarienummer));
}

file sealed class FakeClaudeClient : IClaudeClient
{
    private Beslut? _beslut;
    public int CallCount { get; private set; }
    public void Returns(Beslut b) => _beslut = b;
    public Task<Beslut?> BegarBeslutAsync(Arende arende, string deviceContext, CancellationToken ct = default)
    {
        CallCount++;
        return Task.FromResult(_beslut);
    }
}

file sealed class FakeHomeAssistantClient : IHomeAssistantClient
{
    private readonly Dictionary<string, DeviceState> _states = [];
    public List<string> TurnOnCalls { get; } = [];
    public List<string> TurnOffCalls { get; } = [];
    public List<(string Device, int Percent)> SetBrightnessCalls { get; } = [];

    public bool HasDevice(string friendly) => _states.ContainsKey(friendly);
    public void DeviceIsOff(string friendly) =>
        _states[friendly] = new DeviceState(friendly, $"light.{friendly.ToLower()}", false, null, true);
    public void DeviceIsOn(string friendly, int? brightness = null) =>
        _states[friendly] = new DeviceState(friendly, $"light.{friendly.ToLower()}", true, brightness, true);
    public void DeviceIsUnavailable(string friendly) =>
        _states[friendly] = new DeviceState(friendly, $"light.{friendly.ToLower()}", false, null, false);

    public Task<DeviceState> GetStateAsync(string device, CancellationToken ct = default)
    {
        if (_states.TryGetValue(device, out var s)) return Task.FromResult(s);
        throw new ArgumentException($"Device '{device}' not configured in fake.");
    }
    public Task<HaResult> TurnOnAsync(string device, CancellationToken ct = default)
    {
        TurnOnCalls.Add(device);
        return Task.FromResult<HaResult>(new HaResult.Ok());
    }
    public Task<HaResult> TurnOffAsync(string device, CancellationToken ct = default)
    {
        TurnOffCalls.Add(device);
        return Task.FromResult<HaResult>(new HaResult.Ok());
    }
    public Task<HaResult> SetBrightnessAsync(string device, int percent, CancellationToken ct = default)
    {
        SetBrightnessCalls.Add((device, percent));
        return Task.FromResult<HaResult>(new HaResult.Ok());
    }
    public Task<HaResult> SetVolumeAsync(string device, int percent, CancellationToken ct = default) =>
        Task.FromResult<HaResult>(new HaResult.Ok());
    public Task<HaResult> PlayMediaAsync(string device, string query, CancellationToken ct = default) =>
        Task.FromResult<HaResult>(new HaResult.Ok());
    public Task<IReadOnlyList<McpToolInfo>> ListToolsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<McpToolInfo>>([]);
}
