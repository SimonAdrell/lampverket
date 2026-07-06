namespace Lampverket.Core;

public interface IArendeNotifier
{
    public const string MotiveringPrefix = "Motivering: ";

    Task NotifyAsync(string diarienummer, Arende arende);

    /// <summary>Called when a beslut lands for <paramref name="diarienummer"/>. Dispose to stop listening.</summary>
    IDisposable Subscribe(string diarienummer, Func<Arende, Task> handler);

    /// <summary>
    /// Transient handläggningssteg (e.g. "Granskar hemförhållanden") pushed while the ärende is still
    /// under beredning. Display-only — no diariet write; the beslut/final notification carries the truth.
    /// </summary>
    Task NotifyStegAsync(string diarienummer, string steg);

    /// <summary>Called on each handläggningssteg for <paramref name="diarienummer"/>. Dispose to stop listening.</summary>
    IDisposable SubscribeSteg(string diarienummer, Func<string, Task> handler);
}
