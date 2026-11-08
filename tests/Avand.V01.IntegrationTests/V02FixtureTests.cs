using Avand.V01.IntegrationTests.Fixtures;
using Xunit;

namespace Avand.V01.IntegrationTests;

public sealed class V02FixtureTests(V02Fixture fixture) : IClassFixture<V02Fixture>
{
    [Fact]
    public async Task ExistingTopUpFundsV02Wallet()
    {
        const long funding = 1_500_000;
        var before = await fixture.AvailableAsync(fixture.CustomerId);
        await fixture.FundAsync(funding);
        Assert.Equal(before + funding, await fixture.AvailableAsync(fixture.CustomerId));
    }
}
