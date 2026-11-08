using Avand.V01.IntegrationTests.Fixtures;
using Xunit;

namespace Avand.V01.IntegrationTests;

/// <summary>AC-11 with the specified figures on one store: 1,000,000 mg confirmed custody, then claims of 975,000 mg
/// with a 20,000 mg outgoing commitment, then claims of 995,000 mg with no commitment. Each purchase goes through
/// both real hosts.</summary>
public sealed class V02BackingCapacityTests(V02Fixture fixture) : IClassFixture<V02Fixture>
{
    [Fact]
    public async Task CapacityMatchesTheSpecifiedNormalAndOutageExamples()
    {
        await fixture.FundAsync(1_030_000_000);
        fixture.ObservePrice(1_000_000, 999_000); // 1 mg = 1,000 IRR
        Assert.Equal(1_000_000, await fixture.CustodyMgAsync());

        await fixture.SetBackingAsync(false);
        await BuyAsync(975_000, "Completed");
        Assert.Equal(975_000, await fixture.CustomerClaimsMgAsync());

        // Sample 2: 20,000 mg outgoing commitment and 975,000 mg claims.
        await fixture.SetBackingAsync(false, 20_000);
        Assert.Equal(5_000, await HeadroomAsync());
        Assert.Equal("InsufficientBacking", await RejectedAsync(5_001));
        await fixture.SetBackingAsync(true, 20_000);
        Assert.Equal(14_800, await HeadroomAsync());
        Assert.Equal("InsufficientBacking", await RejectedAsync(14_801));
        await BuyAsync(14_800, "Completed");
        Assert.Equal(0, await HeadroomAsync());

        // Sample 1: no commitment and 995,000 mg claims.
        await fixture.SetBackingAsync(false);
        await BuyAsync(5_200, "Completed");
        Assert.Equal(995_000, await fixture.CustomerClaimsMgAsync());
        Assert.Equal(5_000, await HeadroomAsync());
        await fixture.SetBackingAsync(true);
        Assert.Equal(15_000, await HeadroomAsync());
        Assert.Equal("InsufficientBacking", await RejectedAsync(15_001));
        await BuyAsync(15_000, "Completed");
        Assert.Equal(1_010_000, await fixture.CustomerClaimsMgAsync());
        Assert.Equal(1_000_000, await fixture.CustodyMgAsync());

        // Unreliable custodian state authorizes nothing.
        await fixture.SetBackingAsync(null);
        Assert.Equal("BackingEvidenceUnavailable", await RejectedAsync(1_000));
        await fixture.SetBackingAsync(false);
    }

    private async Task<long> HeadroomAsync() =>
        (await fixture.FinancialStateAsync(fixture.CustomerId)).GetProperty("backing").GetProperty("headroomMg").GetInt64();

    private Task<System.Text.Json.JsonElement> BuyAsync(long mg, string status) => fixture.BuyAsync(mg * 1_000, status);

    private async Task<string?> RejectedAsync(long mg)
    {
        var wallet = await fixture.AvailableAsync(fixture.CustomerId);
        var claims = await fixture.CustomerClaimsMgAsync();
        var order = await BuyAsync(mg, "Rejected");
        Assert.Equal(wallet, await fixture.AvailableAsync(fixture.CustomerId));
        Assert.Equal(claims, await fixture.CustomerClaimsMgAsync());
        return order.GetProperty("reasonCode").GetString();
    }
}
