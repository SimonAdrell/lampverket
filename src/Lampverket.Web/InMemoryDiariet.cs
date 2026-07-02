using Lampverket.Core;

namespace Lampverket.Web;

// Placeholder in-memory diariet for development. Replace with DiariumService from Core.
internal sealed class InMemoryDiariet : IDiariet
{
    private readonly List<Arende> _arenden = [];
    private readonly Lock _lock = new();
    private int _counter;

    public Task AppendAsync(Arende arende)
    {
        lock (_lock) { _arenden.Add(arende); }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Arende>> HamtaAllaAsync()
    {
        lock (_lock) { return Task.FromResult<IReadOnlyList<Arende>>([.. _arenden]); }
    }

    public Task<Arende?> HamtaAsync(string diarienummer)
    {
        Arende? arende;
        lock (_lock) { arende = _arenden.LastOrDefault(a => a.Diarienummer == diarienummer); }
        return Task.FromResult(arende);
    }

    public Task<string> AllokeraDiarienummerAsync(int year)
    {
        var nr = Interlocked.Increment(ref _counter);
        return Task.FromResult($"LV-{year}-{nr:D6}");
    }
}
