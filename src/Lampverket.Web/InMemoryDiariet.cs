using Lampverket.Core;

namespace Lampverket.Web;

// Placeholder in-memory diariet for development. Replace with DiariumService from Core.
internal sealed class InMemoryDiariet : IDiariet
{
    private readonly List<Arende> _arenden = [];

    public Task AppendAsync(Arende arende)
    {
        _arenden.Add(arende);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Arende>> HamtaAllaAsync() =>
        Task.FromResult<IReadOnlyList<Arende>>(_arenden.AsReadOnly());

    public Task<Arende?> HamtaAsync(string diarienummer) =>
        Task.FromResult(_arenden.FirstOrDefault(a => a.Diarienummer == diarienummer));
}
