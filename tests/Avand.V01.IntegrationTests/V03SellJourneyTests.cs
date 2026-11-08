using Avand.V01.IntegrationTests.Fixtures;
using Xunit;

namespace Avand.V01.IntegrationTests;

/// <summary>T010: the V01-funded, V02-bought, V03-sold journey — AC-02, AC-03, AC-05.</summary>
public sealed class V03SellJourneyTests(V03Fixture fixture) : IClassFixture<V03Fixture>
{
    [Fact]
    public async Task PostedSaleMovesExactAmountsAndNetFundsABuyImmediately()
    {
        await fixture.GiveGoldAsync(fixture.CustomerId, 500);
        await fixture.SetFreeLiquidityAsync(10_000_000);
        var gold = await fixture.GoldMgAsync(fixture.CustomerId);
        var wallet = await fixture.AvailableAsync(fixture.CustomerId);

        fixture.ObserveSellPrice();
        var order = await fixture.SellAsync(500, "Completed");

        Assert.Equal(gold - 500, await fixture.GoldMgAsync(fixture.CustomerId));
        Assert.Equal(wallet + 4_875_500, await fixture.AvailableAsync(fixture.CustomerId));
        Assert.Equal(4_875_500, order.GetProperty("netIrr").GetInt64());
        Assert.Equal(10_000_000 - 4_875_500, await fixture.FreeLiquidityAsync());

        var journal = await fixture.SellJournalAsync(order.GetProperty("orderId").GetGuid());
        Assert.NotNull(journal);
        Assert.Equal(5, await fixture.JournalLineCountAsync(journal.Value));
        Assert.Equal(0, await fixture.UnbalancedAssetCountAsync(journal.Value));
        Assert.Equal(1, await fixture.ReadLedgerAsync("""
            SELECT count(*) FROM "JournalLines" l JOIN "Accounts" a ON a."Id" = l."AccountId"
            WHERE l."JournalId" = $1 AND a."Bucket" = 'FeeRevenue' AND NOT l."IsDebit" AND l."AmountMinor" = 24500
            """, journal.Value));

        // AC-03: a V02 buy whose total debit (4,040,000) exceeds the pre-sale wallet is covered by the net.
        Assert.True(wallet < 4_040_000);
        fixture.ObservePrice();
        var buy = await fixture.BuyAsync(4_000_000, "Completed");
        Assert.Equal(4_040_000, buy.GetProperty("totalDebitIrr").GetInt64());
        Assert.Equal(wallet + 4_875_500 - 4_040_000, await fixture.AvailableAsync(fixture.CustomerId));
    }

    [Fact]
    public async Task RestrictionAfterQuoteRejectsWithoutFinancialEffect()
    {
        await fixture.GiveGoldAsync(fixture.OtherId, 500, fixture.Other);
        await fixture.SetFreeLiquidityAsync(10_000_000);
        var gold = await fixture.GoldMgAsync(fixture.OtherId);
        var wallet = await fixture.AvailableAsync(fixture.OtherId);
        var journals = await fixture.JournalCountAsync();

        fixture.ObserveSellPrice();
        var quote = await fixture.SellQuoteAsync(quantityMg: 500, customer: fixture.Other);
        await fixture.RestrictAsync(fixture.OtherId);
        try
        {
            var orderId = await fixture.ConfirmedSellAsync(quote.GetProperty("quoteId").GetGuid(), customer: fixture.Other);
            var order = await fixture.AwaitSellAsync(orderId, "Rejected", fixture.Other);
            Assert.Equal("CustomerNotEligible", order.GetProperty("reasonCode").GetString());
        }
        finally
        {
            await fixture.SetEligibleAsync(fixture.OtherId);
        }

        Assert.Equal(gold, await fixture.GoldMgAsync(fixture.OtherId));
        Assert.Equal(wallet, await fixture.AvailableAsync(fixture.OtherId));
        Assert.Equal(journals, await fixture.JournalCountAsync());
        Assert.Equal(10_000_000, await fixture.FreeLiquidityAsync());
    }
}
