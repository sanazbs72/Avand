using Avand.V01.IntegrationTests.Fixtures;
using Xunit;

namespace Avand.V01.IntegrationTests;

public sealed class V03FixtureTests(V03Fixture fixture) : IClassFixture<V03Fixture>
{
    [Fact]
    public async Task RealTopUpAndBuyGiveCustomerSellableGold()
    {
        await fixture.GiveGoldAsync(fixture.CustomerId, 500);
        Assert.Equal(500, await fixture.GoldMgAsync(fixture.CustomerId));

        await fixture.GiveGoldAsync(fixture.OtherId, 100, fixture.Other);
        Assert.Equal(100, await fixture.GoldMgAsync(fixture.OtherId));
        Assert.Equal(500, await fixture.GoldMgAsync(fixture.CustomerId));
    }
}
