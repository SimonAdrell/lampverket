namespace Lampverket.Core;

public interface IArendeNotifier
{
    Task NotifyAsync(string diarienummer, Arende arende);

    /// <summary>Called when a beslut lands for <paramref name="diarienummer"/>. Dispose to stop listening.</summary>
    IDisposable Subscribe(string diarienummer, Func<Arende, Task> handler);
}
