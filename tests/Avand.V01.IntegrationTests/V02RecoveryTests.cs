using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Avand.V01.IntegrationTests.Fixtures;
using Xunit;

namespace Avand.V01.IntegrationTests;

/// <summary>AC-14 and AC-15: lost replies, Application restart after expiry, unresolved delivery and faults on
/// either side of the Ledger commit. Both stores are read through fresh connections after each fault.</summary>
public sealed class V02RecoveryTests(V02Fixture fixture) : IClassFixture<V02Fixture>
{
    [Fact]
    public async Task ACommittedPurchaseWhoseReplyIsLostRecoversAfterRestartAndExpiry()
    {
        await fixture.FundAsync(2_000_000);
        var wallet = await fixture.AvailableAsync(fixture.CustomerId);
        fixture.ObservePrice();
        var quote = await fixture.QuoteAsync(1_000_000);
        try
        {
            fixture.DropNextPostingResponseThenBlock();
            var orderId = await fixture.ConfirmedOrderAsync(quote.GetProperty("quoteId").GetGuid());
            await V01Fixture.EventuallyAsync(() => Task.FromResult(fixture.DroppedResponse));

            Assert.Equal(1, await fixture.PostedBuyCountAsync(orderId)); // committed before the reply was lost
            var pending = await fixture.Customer.GetFromJsonAsync<JsonElement>($"/api/v1/gold/buy-orders/{orderId}");
            Assert.NotEqual("Completed", pending.GetProperty("status").GetString());

            var later = quote.GetProperty("expiresAt").GetDateTimeOffset() + TimeSpan.FromMinutes(5);
            fixture.ApplicationClock.Set(later);
            fixture.LedgerClock.Set(later);
            await fixture.RestartApplicationAsync();
            fixture.BlockOperations(false);

            var completed = await fixture.AwaitBuyAsync(orderId, "Completed");
            Assert.True(completed.GetProperty("decidedAt").GetDateTimeOffset() < quote.GetProperty("expiresAt").GetDateTimeOffset());
            Assert.Equal(1, await fixture.PostedBuyCountAsync(orderId));
            Assert.Equal(wallet - 1_010_000, await fixture.AvailableAsync(fixture.CustomerId));
        }
        finally
        {
            fixture.BlockOperations(false);
            fixture.ResetClocks();
        }
    }

    [Fact]
    public async Task AFirstDecisionAtExpiryIsADurableRejectionOnReplay()
    {
        await fixture.FundAsync(2_000_000, fixture.Other);
        var wallet = await fixture.AvailableAsync(fixture.OtherId);
        fixture.ObservePrice();
        var quote = await fixture.QuoteAsync(1_000_000, fixture.Other);
        try
        {
            fixture.BlockOperations(true);
            var orderId = await fixture.ConfirmedOrderAsync(quote.GetProperty("quoteId").GetGuid(), customer: fixture.Other);
            await fixture.AwaitBuyAsync(orderId, "FinancialOutcomeUnknown", fixture.Other);
            fixture.LedgerClock.Set(quote.GetProperty("expiresAt").GetDateTimeOffset());
            fixture.BlockOperations(false);
            var rejected = await fixture.AwaitBuyAsync(orderId, "Rejected", fixture.Other);
            Assert.Equal("QuoteExpired", rejected.GetProperty("reasonCode").GetString());

            fixture.LedgerClock.UseSystemTime();
            using var trading = fixture.TradingClient();
            var frozen = await fixture.FrozenRequestAsync(orderId);
            using var replay = await trading.PostAsync("/internal/v1/operations",
                new StringContent(frozen, Encoding.UTF8, "application/json"));
            var result = await replay.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Rejected", result.GetProperty("status").GetString());
            Assert.Equal("QuoteExpired", result.GetProperty("decision").GetProperty("rejectionCode").GetString());
            Assert.Equal(rejected.GetProperty("decidedAt").GetDateTimeOffset(), result.GetProperty("decidedAt").GetDateTimeOffset());
            Assert.Equal(0, await fixture.PostedBuyCountAsync(orderId));
            Assert.Equal(wallet, await fixture.AvailableAsync(fixture.OtherId));
        }
        finally
        {
            fixture.BlockOperations(false);
            fixture.ResetClocks();
        }
    }

    [Fact]
    public async Task AnUnansweredSendStaysUnresolvedWithoutAFinancialEffect()
    {
        await fixture.FundAsync(2_000_000);
        var wallet = await fixture.AvailableAsync(fixture.CustomerId);
        fixture.ObservePrice();
        var quote = await fixture.QuoteAsync(1_000_000);
        try
        {
            fixture.BlockPosting(true); // inquiry reaches the Ledger and gets an ordinary NotFound
            var orderId = await fixture.ConfirmedOrderAsync(quote.GetProperty("quoteId").GetGuid());
            await fixture.AwaitBuyAsync(orderId, "FinancialOutcomeUnknown");
            await V01Fixture.EventuallyAsync(async () => await fixture.ReadApplicationAsync(
                """SELECT "Attempts"::bigint FROM "BuyRecoveryWork" WHERE "OrderId" = $1""", orderId) >= 2);
            Assert.Equal(0, await fixture.ReadLedgerAsync("""SELECT count(*) FROM "BuyDecisions" WHERE "OrderId" = $1""", orderId));
            Assert.Equal(wallet, await fixture.AvailableAsync(fixture.CustomerId));
            fixture.BlockPosting(false);
            await fixture.AwaitBuyAsync(orderId, "Completed");
            Assert.Equal(wallet - 1_010_000, await fixture.AvailableAsync(fixture.CustomerId));
        }
        finally
        {
            fixture.BlockPosting(false);
        }
    }

    [Fact]
    public async Task ALedgerFaultBeforeCommitLeavesNoPartialEffect()
    {
        await fixture.FundAsync(2_000_000, fixture.Other);
        var wallet = await fixture.AvailableAsync(fixture.OtherId);
        var gold = (await fixture.FinancialStateAsync(fixture.OtherId)).GetProperty("availableGoldMg").GetInt64();
        var journals = await fixture.JournalCountAsync();
        fixture.ObservePrice();
        var quote = await fixture.QuoteAsync(1_000_000, fixture.Other);
        await fixture.ArmLedgerBuyFailureAsync();
        try
        {
            var orderId = await fixture.ConfirmedOrderAsync(quote.GetProperty("quoteId").GetGuid(), customer: fixture.Other);
            await fixture.AwaitBuyAsync(orderId, "FinancialOutcomeUnknown", fixture.Other);
            Assert.Equal(0, await fixture.ReadLedgerAsync("""SELECT count(*) FROM "BuyDecisions" WHERE "OrderId" = $1""", orderId));
            Assert.Equal(0, await fixture.ReadLedgerAsync("""SELECT count(*) FROM "BuyQuoteClaims" WHERE "QuoteId" = $1""",
                quote.GetProperty("quoteId").GetGuid()));
            Assert.Equal(journals, await fixture.JournalCountAsync());
            Assert.Equal(wallet, await fixture.AvailableAsync(fixture.OtherId));
            Assert.Equal(gold, (await fixture.FinancialStateAsync(fixture.OtherId)).GetProperty("availableGoldMg").GetInt64());

            await fixture.DisarmLedgerBuyFailureAsync();
            await fixture.AwaitBuyAsync(orderId, "Completed", fixture.Other);
            Assert.Equal(1, await fixture.PostedBuyCountAsync(orderId));
            Assert.Equal(journals + 1, await fixture.JournalCountAsync());
            Assert.Equal(wallet - 1_010_000, await fixture.AvailableAsync(fixture.OtherId));
        }
        finally
        {
            await fixture.DisarmLedgerBuyFailureAsync();
        }
    }
}
