using System.Text.Json;
using Avand.V01.IntegrationTests.Fixtures;
using Xunit;

namespace Avand.V01.IntegrationTests;

/// <summary>AC-09, AC-10 and AC-12: distinct operations racing for one wallet or the shared backing pool, and the
/// advisory maximum. Races go straight to the Ledger boundary from independent sessions while an independent
/// session holds the backing guard, so both decisions are queued before either runs.</summary>
public sealed class V02CapacityTests(V02Fixture fixture) : IClassFixture<V02Fixture>
{
    private const long Ask = 1_000_000; // 1,000 IRR per mg, fee 1% -> 1,010 IRR per mg

    [Fact]
    public async Task TwoBuysRacingForOneWalletPostOnlyOne()
    {
        var customer = fixture.RestrictedId;
        await fixture.SetEligibleAsync(customer);
        await fixture.FundAsync(1_515_000, fixture.Restricted);
        fixture.ObservePrice();
        var first = await fixture.QuoteAsync(1_000_000, fixture.Restricted);
        var second = await fixture.QuoteAsync(1_000_000, fixture.Restricted);

        var results = await RaceAsync(
            V02Fixture.BuyEnvelope(first, customer, Guid.NewGuid(), Guid.NewGuid()),
            V02Fixture.BuyEnvelope(second, customer, Guid.NewGuid(), Guid.NewGuid()));

        Assert.Single(results, x => x.GetProperty("status").GetString() == "Posted");
        var loser = Assert.Single(results, x => x.GetProperty("status").GetString() == "Rejected");
        Assert.Equal("InsufficientRialBalance", loser.GetProperty("decision").GetProperty("rejectionCode").GetString());
        Assert.Equal(505_000, await fixture.AvailableAsync(customer));
        Assert.Equal(100, (await fixture.FinancialStateAsync(customer)).GetProperty("availableGoldMg").GetInt64());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TwoCustomersRacingForSharedBackingPostOnlyOne(bool outage)
    {
        await fixture.FundAsync(1_515_000);
        await fixture.FundAsync(1_515_000, fixture.Other);
        fixture.ObservePrice(Ask, Ask - 1);
        var mine = await fixture.QuoteAsync(1_500_000);
        var theirs = await fixture.QuoteAsync(1_500_000, fixture.Other);
        Assert.Equal(1_500, mine.GetProperty("quantityMg").GetInt64());
        var (remaining, headroom) = await SetHeadroomAsync(outage, 2_000);
        Assert.InRange(headroom, 2_000, 2_001);
        var claimsBefore = await fixture.CustomerClaimsMgAsync();

        var results = await RaceAsync(
            V02Fixture.BuyEnvelope(mine, fixture.CustomerId, Guid.NewGuid(), Guid.NewGuid()),
            V02Fixture.BuyEnvelope(theirs, fixture.OtherId, Guid.NewGuid(), Guid.NewGuid()));

        Assert.Single(results, x => x.GetProperty("status").GetString() == "Posted");
        var loser = Assert.Single(results, x => x.GetProperty("status").GetString() == "Rejected");
        Assert.Equal("InsufficientBacking", loser.GetProperty("decision").GetProperty("rejectionCode").GetString());
        var claims = await fixture.CustomerClaimsMgAsync();
        Assert.Equal(claimsBefore + 1_500, claims);
        Assert.True(claims <= remaining + (outage ? remaining / 100 : 0));
    }

    [Fact]
    public async Task UnavailableEvidenceAuthorizesNoPurchase()
    {
        await fixture.FundAsync(1_010_000);
        fixture.ObservePrice(Ask, Ask - 1);
        var quote = await fixture.QuoteAsync(1_000_000);
        await fixture.SetBackingAsync(null);
        try
        {
            var unavailable = await fixture.QuoteAsync(1_000_000);
            Assert.Equal("Unavailable", unavailable.GetProperty("availability").GetString());
            Assert.Equal(JsonValueKind.Null, unavailable.GetProperty("maximumAvailableMg").ValueKind);
            var result = await fixture.SubmitBuyAsync(
                V02Fixture.BuyEnvelope(quote, fixture.CustomerId, Guid.NewGuid(), Guid.NewGuid()));
            Assert.Equal("BackingEvidenceUnavailable", result.GetProperty("decision").GetProperty("rejectionCode").GetString());
        }
        finally
        {
            await fixture.SetBackingAsync(false);
        }
    }

    [Fact]
    public async Task AdvisoryMaximumIsTheSmallestOfBackingWalletAndOrderCap()
    {
        await fixture.FundAsync(5_000_000);
        fixture.ObservePrice(Ask, Ask - 1);
        try
        {
            var wallet = await fixture.AvailableAsync(fixture.CustomerId);
            var affordable = wallet / 1_010; // independent oracle for this price and the 1% fee

            await SetHeadroomAsync(false, 2_000);
            Assert.Equal(Math.Min(2_000, affordable), await MaximumAsync());

            await SetHeadroomAsync(false, 1_000_000_000);
            Assert.Equal(affordable, await MaximumAsync());

            fixture.SetMaximumIrr(1_500_999);
            Assert.Equal(Math.Min(1_500, affordable), await MaximumAsync());
            fixture.SetMaximumIrr(null);

            await SetHeadroomAsync(false, 999);
            var below = await fixture.QuoteAsync(1_000_000);
            Assert.Equal("NoQuantityAvailable", below.GetProperty("availability").GetString());
            Assert.Equal(0, below.GetProperty("maximumAvailableMg").GetInt64());
            Assert.Contains("InsufficientBacking", below.GetProperty("shortfalls").EnumerateArray().Select(x => x.GetString()));
        }
        finally
        {
            fixture.SetMaximumIrr(null);
            await fixture.SetBackingAsync(false);
        }
    }

    private async Task<long> MaximumAsync()
    {
        var quote = await fixture.QuoteAsync(1_000_000);
        Assert.Equal("Available", quote.GetProperty("availability").GetString());
        return quote.GetProperty("maximumAvailableMg").GetInt64();
    }

    /// <summary>Sets custody evidence so that headroom is <paramref name="target"/> (outage mode: target or target+1,
    /// since floor(E/100) skips values). Returns the remaining eligible custody E and the Ledger-reported headroom.</summary>
    private async Task<(long Remaining, long Headroom)> SetHeadroomAsync(bool outage, long target)
    {
        var custody = await fixture.CustodyMgAsync();
        var claims = await fixture.CustomerClaimsMgAsync();
        var remaining = claims + target;
        if (outage)
        {
            remaining = (claims + target) * 100 / 101;
            while (remaining + remaining / 100 < claims + target)
                remaining++;
        }

        var outgoing = Math.Max(0, custody - remaining);
        await fixture.SetBackingAsync(outage, outgoing);
        var state = await fixture.FinancialStateAsync(fixture.CustomerId);
        return (custody - outgoing, state.GetProperty("backing").GetProperty("headroomMg").GetInt64());
    }

    private async Task<JsonElement[]> RaceAsync(object first, object second)
    {
        Task<JsonElement>[] racing;
        await using (await fixture.HoldBackingAsync())
        {
            racing = [fixture.SubmitBuyAsync(first), fixture.SubmitBuyAsync(second)];
            await fixture.AwaitBlockedLedgerSessionsAsync(2);
        }
        return await Task.WhenAll(racing);
    }
}
