using Avand.V01.IntegrationTests.Fixtures;
using Xunit;

namespace Avand.V01.IntegrationTests;

public sealed class LedgerRecoveryTests(V01Fixture fixture) : IClassFixture<V01Fixture>
{
    [Fact]
    public async Task VerifiedEvidenceRecoversAfterWindowAndLostResponse()
    {
        var before = await fixture.AvailableAsync(fixture.CustomerId);
        fixture.BlockPosting(true);
        var id = await fixture.StartAsync();
        await fixture.AwaitStatusAsync(id, "AwaitingProvider");
        fixture.Gateway.SetResult(id, "Captured");
        await fixture.NotifyAsync(id);
        await fixture.AwaitStatusAsync(id, "VerifiedPendingLedger");
        fixture.Gateway.Advance(TimeSpan.FromMinutes(16));
        Assert.Equal(before, await fixture.AvailableAsync(fixture.CustomerId));
        Assert.Equal(0L, await fixture.PostingCountAsync(id));

        fixture.DropNextPostingResponse();
        fixture.BlockPosting(false);
        await V01Fixture.EventuallyAsync(() => Task.FromResult(fixture.DroppedResponse));
        Assert.Equal(1L, await fixture.PostingCountAsync(id));
        await fixture.RestartApplicationAsync();
        await fixture.AwaitStatusAsync(id, "Succeeded");
        Assert.Equal(before + 100, await fixture.AvailableAsync(fixture.CustomerId));
        Assert.Equal(1L, await fixture.PostingCountAsync(id));
    }

    [Fact]
    public async Task FailedCommitLeavesNoPartialPosting()
    {
        var before = await fixture.AvailableAsync(fixture.CustomerId);
        var journals = await fixture.JournalCountAsync();
        await fixture.ArmCommitFailureAsync();
        try
        {
            var id = await fixture.StartAsync();
            await fixture.AwaitStatusAsync(id, "AwaitingProvider");
            fixture.Gateway.SetResult(id, "Captured");
            await fixture.NotifyAsync(id);
            await fixture.AwaitStatusAsync(id, "VerifiedPendingLedger");
            await V01Fixture.EventuallyAsync(async () => await fixture.LedgerAttemptsAsync(id) >= 2);
            Assert.Equal(before, await fixture.AvailableAsync(fixture.CustomerId));
            Assert.Equal(journals, await fixture.JournalCountAsync());
            Assert.Equal(0L, await fixture.DecisionCountAsync(id));
            await fixture.DisarmCommitFailureAsync();
            await fixture.AwaitStatusAsync(id, "Succeeded");
            Assert.Equal(before + 100, await fixture.AvailableAsync(fixture.CustomerId));
            Assert.Equal(1L, await fixture.PostingCountAsync(id));
        }
        finally
        {
            await fixture.DisarmCommitFailureAsync();
        }
    }
}
