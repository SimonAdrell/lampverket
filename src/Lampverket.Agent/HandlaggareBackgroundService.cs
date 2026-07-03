using System.Threading.Channels;
using Lampverket.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Lampverket.Agent;

public sealed class HandlaggareBackgroundService(
    IArendeProcessor handlaggare,
    ChannelReader<string> reader,
    ILogger<HandlaggareBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await foreach (var diarienummer in reader.ReadAllAsync(ct))
        {
            try
            {
                await handlaggare.ProcessArendeAsync(diarienummer, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Handläggningsfel för {Diarienummer}", diarienummer);
            }
        }
    }
}
