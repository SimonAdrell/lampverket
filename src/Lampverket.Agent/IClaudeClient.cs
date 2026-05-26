namespace Lampverket.Agent;

using Lampverket.Core;

public interface IClaudeClient
{
    Task<Beslut?> BegarBeslutAsync(Arende arende, string deviceContext, CancellationToken ct = default);
}
