using Lampverket.Core;

namespace Lampverket.Agent;

public interface IHandlaggareAgent
{
    Task<Handlaggningsresultat> HandlaggaAsync(
        Arende arende, IProgress<Handlaggningshandelse>? progress = null, CancellationToken ct = default);
}
