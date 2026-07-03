namespace Lampverket.Core;

public interface IArendeProcessor
{
    Task ProcessArendeAsync(string diarienummer, CancellationToken ct = default);
}
