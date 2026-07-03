using Lampverket.Core;

namespace Lampverket.Agent;

public interface IHandlaggareAgent
{
    Task<Handlaggningsresultat> HandlaggaAsync(Arende arende, CancellationToken ct = default);
}
