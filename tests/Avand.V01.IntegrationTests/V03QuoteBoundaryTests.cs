using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Avand.V01.IntegrationTests.Fixtures;
using Xunit;

namespace Avand.V01.IntegrationTests;

/// <summary>T017 / AC-06: quote expiry is judged at the first protected Ledger decision, an expired order offers a
/// refresh with the original input that needs a new quote and confirmation, and no price means no quote.</summary>
public sealed class V03QuoteBoundaryTests(V03Fixture fixture) : IClassFixture<V03Fixture>
{
    private static readonly DateTimeOffset T = new(2026, 11, 8, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task FirstLedgerDecisionPostsJustBeforeExpiryAndRejectsAtExpiry()
    {
        await fixture.GiveGoldAsync(fixture.CustomerId, 1000);
        await fixture.SetFreeLiquidityAsync(50_000_000);
        var gold = await fixture.GoldMgAsync(fixture.CustomerId);
        var wallet = await fixture.AvailableAsync(fixture.CustomerId);
        try
        {
            fixture.ApplicationClock.Set(T);
            fixture.ObserveSellPrice();
            var early = await fixture.SellQuoteAsync(quantityMg: 500);
            var late = await fixture.SellQuoteAsync(quantityMg: 500);
            Assert.Equal(T + Lifetime, early.GetProperty("expiresAt").GetDateTimeOffset());

            fixture.LedgerClock.Set(T + Lifetime - TimeSpan.FromMicroseconds(1));
            var posted = await fixture.AwaitSellAsync(
                await fixture.ConfirmedSellAsync(early.GetProperty("quoteId").GetGuid()), "Completed");
            Assert.Equal(T + Lifetime - TimeSpan.FromMicroseconds(1), posted.GetProperty("decidedAt").GetDateTimeOffset());
            Assert.Equal(wallet + 4_875_500, await fixture.AvailableAsync(fixture.CustomerId));

            fixture.LedgerClock.Set(T + Lifetime);
            var rejected = await fixture.AwaitSellAsync(
                await fixture.ConfirmedSellAsync(late.GetProperty("quoteId").GetGuid()), "Rejected");
            Assert.Equal("QuoteExpired", rejected.GetProperty("reasonCode").GetString());
            var refresh = rejected.GetProperty("refresh");
            Assert.Equal("RequestNewQuote", refresh.GetProperty("action").GetString());
            Assert.Equal("/api/v1/gold/sell-quotes", refresh.GetProperty("href").GetString());
            Assert.Equal(500, refresh.GetProperty("quantityMg").GetInt64());
            Assert.Equal(JsonValueKind.Null, refresh.GetProperty("requestedGrossIrr").ValueKind);
            Assert.Equal(gold - 500, await fixture.GoldMgAsync(fixture.CustomerId));
            Assert.Equal(wallet + 4_875_500, await fixture.AvailableAsync(fixture.CustomerId));

            // The refresh is a new quote with its own identity and needs its own explicit confirmation.
            fixture.ApplicationClock.Set(T + TimeSpan.FromSeconds(31));
            fixture.ObserveSellPrice();
            var replacement = await fixture.SellQuoteAsync(quantityMg: refresh.GetProperty("quantityMg").GetInt64());
            Assert.NotEqual(late.GetProperty("quoteId").GetGuid(), replacement.GetProperty("quoteId").GetGuid());
            Assert.Equal(gold - 500, await fixture.GoldMgAsync(fixture.CustomerId));
            fixture.LedgerClock.Set(T + TimeSpan.FromSeconds(32));
            await fixture.AwaitSellAsync(
                await fixture.ConfirmedSellAsync(replacement.GetProperty("quoteId").GetGuid()), "Completed");
            Assert.Equal(gold - 1000, await fixture.GoldMgAsync(fixture.CustomerId));
            Assert.Equal(wallet + 2 * 4_875_500, await fixture.AvailableAsync(fixture.CustomerId));
        }
        finally
        {
            fixture.ResetClocks();
        }
    }

    [Fact]
    public async Task AnExpiredGrossOrderRefreshesWithTheOriginalGrossTarget()
    {
        var wallet = await fixture.AvailableAsync(fixture.OtherId);
        try
        {
            fixture.ApplicationClock.Set(T);
            fixture.ObserveSellPrice();
            var quote = await fixture.SellQuoteAsync(requestedGrossIrr: 4_900_000, customer: fixture.Other);
            fixture.LedgerClock.Set(T + Lifetime);
            var rejected = await fixture.AwaitSellAsync(await fixture.ConfirmedSellAsync(
                quote.GetProperty("quoteId").GetGuid(), customer: fixture.Other), "Rejected", fixture.Other);
            Assert.Equal("QuoteExpired", rejected.GetProperty("reasonCode").GetString());
            var refresh = rejected.GetProperty("refresh");
            Assert.Equal(4_900_000, refresh.GetProperty("requestedGrossIrr").GetInt64());
            Assert.Equal(JsonValueKind.Null, refresh.GetProperty("quantityMg").ValueKind);
            Assert.Equal(wallet, await fixture.AvailableAsync(fixture.OtherId));
        }
        finally
        {
            fixture.ResetClocks();
        }
    }

    [Fact]
    public async Task NoCurrentPriceMeansNoQuote()
    {
        foreach (var observed in new[] { null, "not-a-time" })
        {
            fixture.SetPrice(V03Fixture.Ask, V03Fixture.Bid, observed);
            using var reply = await fixture.RequestSellQuoteAsync(new { quantityMg = 500 });
            Assert.Equal(HttpStatusCode.ServiceUnavailable, reply.StatusCode);
            var body = await reply.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("PriceUnavailable", body.GetProperty("code").GetString());
            Assert.True(body.GetProperty("retryable").GetBoolean());
        }
        fixture.ObserveSellPrice();
    }
}
