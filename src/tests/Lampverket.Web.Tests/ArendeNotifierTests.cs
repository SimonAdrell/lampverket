using Lampverket.Core;

namespace Lampverket.Web.Tests;

public class ArendeNotifierTests
{
    [Fact]
    public async Task SubscribeSteg_DisposeOnlyRemovesMatchingHandler()
    {
        var sut = new ArendeNotifier();
        var firstCalls = new List<string>();
        var secondCalls = new List<string>();

        var first = sut.SubscribeSteg("LV-2026-000001", value =>
        {
            firstCalls.Add(value);
            return Task.CompletedTask;
        });
        sut.SubscribeSteg("LV-2026-000001", value =>
        {
            secondCalls.Add(value);
            return Task.CompletedTask;
        });

        first.Dispose();
        await sut.NotifyStegAsync("LV-2026-000001", "Motivering");

        Assert.Empty(firstCalls);
        Assert.Equal(["Motivering"], secondCalls);
    }
}
