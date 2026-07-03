using System.Collections.Concurrent;

namespace Lampverket.Core;

/// <summary>
/// In-process notifier. The background handläggare publishes the finished ärende; the ärende page
/// subscribes and re-renders over its existing Blazor circuit. No hub, no network round-trip.
/// </summary>
public sealed class ArendeNotifier : IArendeNotifier
{
    // ponytail: one subscriber per diarienummer (the last open page wins). Make the value a list if
    // the same ärende must update in several tabs at once. Dispose is identity-safe (below) so a
    // closing page never evicts a newer page's handler.
    private readonly ConcurrentDictionary<string, Func<Arende, Task>> _subscribers = new();
    private readonly ConcurrentDictionary<string, Func<string, Task>> _stegSubscribers = new();

    public Task NotifyAsync(string diarienummer, Arende arende) =>
        _subscribers.TryGetValue(diarienummer, out var handler) ? handler(arende) : Task.CompletedTask;

    public IDisposable Subscribe(string diarienummer, Func<Arende, Task> handler)
    {
        _subscribers[diarienummer] = handler;
        // Remove only if this exact handler is still registered — a later Subscribe for the same
        // diarienummer must not be torn down when an earlier page disposes.
        return new Subscription(() =>
            _subscribers.TryRemove(new KeyValuePair<string, Func<Arende, Task>>(diarienummer, handler)));
    }

    public Task NotifyStegAsync(string diarienummer, string steg) =>
        _stegSubscribers.TryGetValue(diarienummer, out var handler) ? handler(steg) : Task.CompletedTask;

    public IDisposable SubscribeSteg(string diarienummer, Func<string, Task> handler)
    {
        _stegSubscribers[diarienummer] = handler;
        return new Subscription(() =>
            _stegSubscribers.TryRemove(new KeyValuePair<string, Func<string, Task>>(diarienummer, handler)));
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}
