using Avand.V01.IntegrationTests.Fixtures;
using Xunit;

namespace Avand.V01.IntegrationTests;

public sealed class RestrictedCreditTests(V01Fixture fixture) : IClassFixture<V01Fixture>
{
    [Fact]
    public async Task RestrictionBeforeFirstPostingCreditsRestrictedBucket()
    {
        var id = await fixture.StartAsync();
        await fixture.AwaitStatusAsync(id, "AwaitingProvider");
        await fixture.RestrictAsync(fixture.CustomerId);
        fixture.Gateway.SetResult(id, "Captured");
        await Task.WhenAll(fixture.NotifyAsync(id), fixture.NotifyAsync(id));
        var result = await fixture.AwaitStatusAsync(id, "Succeeded");
        Assert.Equal("CreditRestricted", result.GetProperty("messageCode").GetString());
        Assert.Equal(100L, await fixture.RestrictedAsync(fixture.CustomerId));
        Assert.Equal(0L, await fixture.AvailableAsync(fixture.CustomerId));
        Assert.Equal(1L, await fixture.PostingCountAsync(id));
        var second = await fixture.StartAsync();
        await fixture.AwaitStatusAsync(second, "InitiationRejected");
    }
}
