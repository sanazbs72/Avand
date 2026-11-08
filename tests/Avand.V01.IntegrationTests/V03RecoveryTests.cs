using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Avand.V01.IntegrationTests.Fixtures;
using Xunit;

namespace Avand.V01.IntegrationTests;

/// <summary>T022 / AC-13: lost replies, Application restart after expiry, faults on either side of the Ledger
/// commit and an unreachable Ledger. While unresolved the order is never Completed or Rejected; both stores are
/// read through fresh connections after each fault.</summary>
public sealed class V03RecoveryTests(V03Fixture fixture) : IClassFixture<V03Fixture>
{
    [Fact]
    public async Task ACommittedSaleWhoseReplyIsLostRecoversAfterRestartAndExpiry()
    {
        await fixture.GiveGoldAsync(fixture.CustomerId, 500);
        await fixture.SetFreeLiquidityAsync(50_000_000);
        var wallet = await fixture.AvailableAsync(fixture.CustomerId);
        fixture.ObserveSellPrice();
        var quote = await fixture.SellQuoteAsync(quantityMg: 500);
        try
        {
            fixture.DropNextPostingResponseThenBlock();
            var orderId = await fixture.ConfirmedSellAsync(quote.GetProperty("quoteId").GetGuid());
            await V01Fixture.EventuallyAsync(() => Task.FromResult(fixture.DroppedResponse));

            Assert.Equal(1, await fixture.PostedSellCountAsync(orderId)); // committed before the reply was lost
            await AssertUnresolvedAsync(orderId);

            var later = quote.GetProperty("expiresAt").GetDateTimeOffset() + TimeSpan.FromMinutes(5);
            fixture.ApplicationClock.Set(later);
            fixture.LedgerClock.Set(later);
            await fixture.RestartApplicationAsync();
            fixture.BlockOperations(false);

            var completed = await fixture.AwaitSellAsync(orderId, "Completed");
            Assert.True(completed.GetProperty("decidedAt").GetDateTimeOffset() < quote.GetProperty("expiresAt").GetDateTimeOffset());
            Assert.Equal(1, await fixture.PostedSellCountAsync(orderId));
            Assert.Equal(wallet + 4_875_500, await fixture.AvailableAsync(fixture.CustomerId));
        }
        finally
        {
            fixture.BlockOperations(false);
            fixture.ResetClocks();
        }
    }

    [Fact]
    public async Task AnUnansweredSendStaysUnresolvedAndBalancesReadAsUnavailable()
    {
        await fixture.GiveGoldAsync(fixture.OtherId, 500, fixture.Other);
        await fixture.SetFreeLiquidityAsync(50_000_000);
        var gold = await fixture.GoldMgAsync(fixture.OtherId);
        var wallet = await fixture.AvailableAsync(fixture.OtherId);
        fixture.ObserveSellPrice();
        var quote = await fixture.SellQuoteAsync(quantityMg: 500, customer: fixture.Other);
        try
        {
            fixture.BlockOperations(true);
            var orderId = await fixture.ConfirmedSellAsync(quote.GetProperty("quoteId").GetGuid(), customer: fixture.Other);
            await fixture.AwaitSellAsync(orderId, "FinancialOutcomeUnknown", fixture.Other);
            await V01Fixture.EventuallyAsync(async () => await fixture.ReadApplicationAsync(
                """SELECT "Attempts"::bigint FROM "SellRecoveryWork" WHERE "OrderId" = $1""", orderId) >= 2);
            Assert.Equal(0, await fixture.SellCountAsync("\"OrderId\" = $1", orderId));

            // An unreachable Ledger reads as unavailable, never as a zero balance.
            using (var balances = await fixture.Other.GetAsync("/api/v1/gold/balances"))
            {
                Assert.Equal(HttpStatusCode.ServiceUnavailable, balances.StatusCode);
                var body = await balances.Content.ReadFromJsonAsync<JsonElement>();
                Assert.Equal("BalanceUnavailable", body.GetProperty("code").GetString());
            }
            Assert.Equal(gold, await fixture.GoldMgAsync(fixture.OtherId));

            fixture.BlockOperations(false);
            await fixture.AwaitSellAsync(orderId, "Completed", fixture.Other);
            Assert.Equal(wallet + 4_875_500, await fixture.AvailableAsync(fixture.OtherId));
        }
        finally
        {
            fixture.BlockOperations(false);
        }
    }

    [Fact]
    public async Task ALedgerFaultBeforeCommitLeavesNoPartialEffect()
    {
        await fixture.GiveGoldAsync(fixture.CustomerId, 500);
        await fixture.SetFreeLiquidityAsync(50_000_000);
        var gold = await fixture.GoldMgAsync(fixture.CustomerId);
        var wallet = await fixture.AvailableAsync(fixture.CustomerId);
        var liquidity = await fixture.FreeLiquidityAsync();
        var journals = await fixture.JournalCountAsync();
        fixture.ObserveSellPrice();
        var quote = await fixture.SellQuoteAsync(quantityMg: 500);
        var quoteId = quote.GetProperty("quoteId").GetGuid();
        await fixture.ArmLedgerSellFailureAsync();
        try
        {
            var orderId = await fixture.ConfirmedSellAsync(quoteId);
            await fixture.AwaitSellAsync(orderId, "FinancialOutcomeUnknown");
            Assert.Equal(0, await fixture.SellCountAsync("\"OrderId\" = $1", orderId));
            Assert.Equal(0, await fixture.ReadLedgerAsync("""SELECT count(*) FROM "SellQuoteClaims" WHERE "QuoteId" = $1""", quoteId));
            Assert.Equal(journals, await fixture.JournalCountAsync());
            Assert.Equal(gold, await fixture.GoldMgAsync(fixture.CustomerId));
            Assert.Equal(wallet, await fixture.AvailableAsync(fixture.CustomerId));
            Assert.Equal(liquidity, await fixture.FreeLiquidityAsync());

            await fixture.DisarmLedgerSellFailureAsync();
            await fixture.AwaitSellAsync(orderId, "Completed");
            Assert.Equal(1, await fixture.PostedSellCountAsync(orderId));
            Assert.Equal(journals + 1, await fixture.JournalCountAsync());
            Assert.Equal(wallet + 4_875_500, await fixture.AvailableAsync(fixture.CustomerId));
        }
        finally
        {
            await fixture.DisarmLedgerSellFailureAsync();
        }
    }

    [Fact]
    public async Task AnApplicationApplyFaultAfterCommitRepairsForwardWithoutASecondSale()
    {
        await fixture.GiveGoldAsync(fixture.OtherId, 500, fixture.Other);
        await fixture.SetFreeLiquidityAsync(50_000_000);
        var wallet = await fixture.AvailableAsync(fixture.OtherId);
        var journals = await fixture.JournalCountAsync();
        fixture.ObserveSellPrice();
        var quote = await fixture.SellQuoteAsync(quantityMg: 500, customer: fixture.Other);
        await fixture.ArmApplicationSellApplyFailureAsync();
        try
        {
            var orderId = await fixture.ConfirmedSellAsync(quote.GetProperty("quoteId").GetGuid(), customer: fixture.Other);
            await V01Fixture.EventuallyAsync(async () => await fixture.PostedSellCountAsync(orderId) == 1);
            await V01Fixture.EventuallyAsync(async () => await fixture.ReadApplicationAsync(
                """SELECT "Attempts"::bigint FROM "SellRecoveryWork" WHERE "OrderId" = $1""", orderId) >= 2);
            await AssertUnresolvedAsync(orderId, fixture.Other);
            Assert.Equal(wallet + 4_875_500, await fixture.AvailableAsync(fixture.OtherId));

            await fixture.DisarmApplicationSellApplyFailureAsync();
            await fixture.AwaitSellAsync(orderId, "Completed", fixture.Other);
            Assert.Equal(1, await fixture.PostedSellCountAsync(orderId));
            Assert.Equal(journals + 1, await fixture.JournalCountAsync());
            Assert.Equal(wallet + 4_875_500, await fixture.AvailableAsync(fixture.OtherId));

            // A replay of the frozen request returns the stored decision without a second journal.
            using var trading = fixture.TradingClient();
            var frozen = await fixture.FrozenSellRequestAsync(orderId);
            using var replay = await trading.PostAsync("/internal/v1/operations",
                new StringContent(frozen, Encoding.UTF8, "application/json"));
            Assert.Equal("Posted", (await replay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
            Assert.Equal(journals + 1, await fixture.JournalCountAsync());
        }
        finally
        {
            await fixture.DisarmApplicationSellApplyFailureAsync();
        }
    }

    private async Task AssertUnresolvedAsync(Guid orderId, HttpClient? customer = null)
    {
        var pending = await (customer ?? fixture.Customer).GetFromJsonAsync<JsonElement>($"/api/v1/gold/sell-orders/{orderId}");
        Assert.Contains(pending.GetProperty("status").GetString(), new[] { "Processing", "FinancialOutcomeUnknown" });
    }
}
