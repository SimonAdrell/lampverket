using Lampverket.Core;

namespace Lampverket.Web;

// Placeholder in-memory diariet for development. Replace with DiariumService from Core.
internal sealed class InMemoryDiariet : IDiariet
{
    private readonly List<Arende> _arenden = [];
    private readonly Lock _lock = new();

    public Task AppendAsync(Arende arende)
    {
        lock (_lock) { _arenden.Add(arende); }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Arende>> HamtaAllaAsync()
    {
        List<Arende> snapshot;
        lock (_lock) { snapshot = [.. _arenden]; }
        return Task.FromResult<IReadOnlyList<Arende>>(snapshot.AsReadOnly());
    }

    public Task<Arende?> HamtaAsync(string diarienummer)
    {
        Arende? arende;
        lock (_lock) { arende = _arenden.LastOrDefault(a => a.Diarienummer == diarienummer); }
        return Task.FromResult(arende);
    }
}
