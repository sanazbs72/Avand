using Avand.V01.IntegrationTests.Fixtures;
using Xunit;

namespace Avand.V01.IntegrationTests;

/// <summary>T018 / AC-07–AC-09: insufficient gold and liquidity at the exact boundary, liquidity falling between
/// issuance and decision, H/R and receivables, negative liquidity and a recomputation mismatch. Each rejection
/// leaves zero journal lines, unchanged balances and a stored reason.</summary>
public sealed class V03RejectionTests(V03Fixture fixture) : IClassFixture<V03Fixture>
{
    [Fact]
    public async Task GoldBoundaryRejectsFiveHundredAgainstFourNinetyNineAndPostsFourNinetyNine()
    {
        await fixture.GiveGoldAsync(fixture.CustomerId, 499);
        await fixture.SetFreeLiquidityAsync(50_000_000);
        var gold = await fixture.GoldMgAsync(fixture.CustomerId);
        var wallet = await fixture.AvailableAsync(fixture.CustomerId);
        var journals = await fixture.JournalCountAsync();
        Assert.Equal(499, gold);

        fixture.ObserveSellPrice();
        var rejected = await fixture.SellAsync(500, "Rejected");
        Assert.Equal("InsufficientGold", rejected.GetProperty("reasonCode").GetString());
        await AssertNoEffectAsync(rejected, fixture.CustomerId, gold, wallet, journals, "InsufficientGold");

        var posted = await fixture.SellAsync(499, "Completed");
        Assert.Equal(0, await fixture.GoldMgAsync(fixture.CustomerId));
        Assert.Equal(wallet + posted.GetProperty("netIrr").GetInt64(), await fixture.AvailableAsync(fixture.CustomerId));
    }

    [Fact]
    public async Task LiquidityBoundaryUsesTheNetAndFollowsHAndR()
    {
        await fixture.GiveGoldAsync(fixture.OtherId, 500, fixture.Other);
        var gold = await fixture.GoldMgAsync(fixture.OtherId);
        var wallet = await fixture.AvailableAsync(fixture.OtherId);
        fixture.ObserveSellPrice();

        // One IRR below the net (4,875,500) rejects, although it exceeds nothing near the gross (4,900,000).
        await fixture.SetFreeLiquidityAsync(4_875_499);
        var journals = await fixture.JournalCountAsync();
        var shortByOne = await fixture.SellAsync(500, "Rejected", fixture.Other);
        Assert.Equal("SalesTemporarilyUnavailable", shortByOne.GetProperty("reasonCode").GetString());
        await AssertNoEffectAsync(shortByOne, fixture.OtherId, gold, wallet, journals, "InsufficientLiquidity");

        // A higher H or R lowers free liquidity by exactly the change.
        await fixture.SetFreeLiquidityAsync(4_875_500);
        var r = await fixture.ReadLedgerAsync("""SELECT "BufferIrr" FROM "LiquidityControl" WHERE "Id" = 1""");
        await fixture.SetLiquidityControlAsync(7, r);
        Assert.Equal(4_875_500 - 7, await fixture.FreeLiquidityAsync());
        await fixture.SetLiquidityControlAsync(0, r + 7);
        Assert.Equal(4_875_500 - 7, await fixture.FreeLiquidityAsync());

        // Liquidity that drops after issuance rejects at the Ledger decision.
        await fixture.SetFreeLiquidityAsync(4_875_500);
        var quote = await fixture.SellQuoteAsync(quantityMg: 500, customer: fixture.Other);
        var buffer = await fixture.ReadLedgerAsync("""SELECT "BufferIrr" FROM "LiquidityControl" WHERE "Id" = 1""");
        await fixture.SetLiquidityControlAsync(0, buffer + 1);
        var dropped = await fixture.AwaitSellAsync(await fixture.ConfirmedSellAsync(
            quote.GetProperty("quoteId").GetGuid(), customer: fixture.Other), "Rejected", fixture.Other);
        await AssertNoEffectAsync(dropped, fixture.OtherId, gold, wallet, journals, "InsufficientLiquidity");

        // Negative free liquidity rejects too.
        await fixture.SetLiquidityControlAsync(0, buffer + 10_000_000);
        Assert.True(await fixture.FreeLiquidityAsync() < 0);
        var negative = await fixture.SellAsync(500, "Rejected", fixture.Other);
        await AssertNoEffectAsync(negative, fixture.OtherId, gold, wallet, journals, "InsufficientLiquidity");

        // Exactly the net posts and leaves zero.
        await fixture.SetFreeLiquidityAsync(4_875_500);
        await fixture.SellAsync(500, "Completed", fixture.Other);
        Assert.Equal(0, await fixture.FreeLiquidityAsync());
        Assert.Equal(wallet + 4_875_500, await fixture.AvailableAsync(fixture.OtherId));
    }

    [Fact]
    public async Task AV01GatewayReceivableAddsNoLiquidity()
    {
        await fixture.SetFreeLiquidityAsync(10_000_000);
        await fixture.FundAsync(1_000_000, fixture.Other);
        // The credit raises a customer claim, while the receivable is not cash.
        Assert.Equal(9_000_000, await fixture.FreeLiquidityAsync());
    }

    [Fact]
    public async Task ASnapshotWhoseFeeDoesNotRecomputeIsQuoteTermsInvalid()
    {
        var journals = await fixture.JournalCountAsync();
        var wallet = await fixture.AvailableAsync(fixture.CustomerId);
        var order = Guid.NewGuid();
        var issued = DateTimeOffset.UtcNow.AddSeconds(-1);
        issued = new DateTimeOffset(issued.UtcTicks - issued.UtcTicks % 10, TimeSpan.Zero);
        var result = await fixture.SubmitBuyAsync(V03Fixture.SellEnvelope(fixture.CustomerId, order, Guid.NewGuid(),
            Guid.NewGuid(), issued, 500, 4_900_000, 24_499, 4_875_501));
        Assert.Equal("Rejected", result.GetProperty("status").GetString());
        Assert.Equal("QuoteTermsInvalid", result.GetProperty("decision").GetProperty("rejectionCode").GetString());
        Assert.Equal(journals, await fixture.JournalCountAsync());
        Assert.Equal(wallet, await fixture.AvailableAsync(fixture.CustomerId));
        Assert.Equal(1, await fixture.SellCountAsync("\"OrderId\" = $1 AND \"RejectionCode\" = 'QuoteTermsInvalid'", order));
    }

    private async Task AssertNoEffectAsync(System.Text.Json.JsonElement order, Guid customer, long gold, long wallet,
        long journals, string storedReason)
    {
        var orderId = order.GetProperty("orderId").GetGuid();
        Assert.Null(await fixture.SellJournalAsync(orderId));
        Assert.Equal(1, await fixture.SellCountAsync("\"OrderId\" = $1 AND \"RejectionCode\" = $2", orderId, storedReason));
        Assert.Equal(gold, await fixture.GoldMgAsync(customer));
        Assert.Equal(wallet, await fixture.AvailableAsync(customer));
        Assert.Equal(journals, await fixture.JournalCountAsync());
    }
}
