using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Avand.V01.IntegrationTests.Fixtures;
using Xunit;

namespace Avand.V01.IntegrationTests;

/// <summary>AC-07 and AC-08: provider price age at issue and quote expiry at the first protected Ledger decision.</summary>
public sealed class V02QuoteBoundaryTests(V02Fixture fixture) : IClassFixture<V02Fixture>
{
    private static readonly DateTimeOffset T = new(2026, 11, 8, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task ProviderAgeIsInclusiveAtThirtySecondsAndInvalidTimesBlockIssuance()
    {
        try
        {
            fixture.ApplicationClock.Set(T);
            fixture.SetPrice(10_000_000, 9_800_000, V02Fixture.Iso(T - Lifetime));
            var quote = await fixture.QuoteAsync(1_000_000);
            Assert.Equal(T, quote.GetProperty("issuedAt").GetDateTimeOffset());
            Assert.Equal(T + Lifetime, quote.GetProperty("expiresAt").GetDateTimeOffset());

            foreach (var observed in new[]
            {
                V02Fixture.Iso(T - Lifetime - TimeSpan.FromTicks(1)), null, "not-a-time",
                V02Fixture.Iso(T + TimeSpan.FromTicks(1)), "2026-11-08T12:30:00.0000000+03:30"
            })
            {
                fixture.SetPrice(10_000_000, 9_800_000, observed);
                using var reply = await fixture.Customer.PostAsJsonAsync("/api/v1/gold/buy-quotes",
                    new { requestedPrincipalIrr = 1_000_000 });
                Assert.Equal(HttpStatusCode.ServiceUnavailable, reply.StatusCode);
                var body = await reply.Content.ReadFromJsonAsync<JsonElement>();
                Assert.Equal("PriceUnavailable", body.GetProperty("code").GetString());
            }
        }
        finally
        {
            fixture.ResetClocks();
        }
    }

    [Fact]
    public async Task FirstLedgerDecisionPostsJustBeforeExpiryAndRejectsAtExpiry()
    {
        await fixture.FundAsync(5_000_000);
        try
        {
            fixture.ApplicationClock.Set(T);
            fixture.ObservePrice();
            var early = await fixture.QuoteAsync(1_000_000);
            var late = await fixture.QuoteAsync(1_000_000);

            fixture.LedgerClock.Set(T + Lifetime - TimeSpan.FromMicroseconds(1));
            var posted = await fixture.AwaitBuyAsync(
                await fixture.ConfirmedOrderAsync(early.GetProperty("quoteId").GetGuid()), "Completed");
            Assert.Equal(T + Lifetime - TimeSpan.FromMicroseconds(1), posted.GetProperty("decidedAt").GetDateTimeOffset());

            // Confirmed before the boundary; the first Ledger decision lands exactly on it.
            var wallet = await fixture.AvailableAsync(fixture.CustomerId);
            fixture.LedgerClock.Set(T + Lifetime);
            var rejected = await fixture.AwaitBuyAsync(
                await fixture.ConfirmedOrderAsync(late.GetProperty("quoteId").GetGuid()), "Rejected");
            Assert.Equal("QuoteExpired", rejected.GetProperty("reasonCode").GetString());
            var refresh = rejected.GetProperty("refresh");
            Assert.Equal("RequestNewQuote", refresh.GetProperty("action").GetString());
            Assert.Equal(1_000_000, refresh.GetProperty("requestedPrincipalIrr").GetInt64());
            Assert.Equal(wallet, await fixture.AvailableAsync(fixture.CustomerId));

            // A replacement quote has its own identity and terms and needs a new explicit confirmation.
            fixture.ApplicationClock.Set(T + TimeSpan.FromSeconds(31));
            fixture.ObservePrice();
            var replacement = await fixture.QuoteAsync(refresh.GetProperty("requestedPrincipalIrr").GetInt64());
            Assert.NotEqual(late.GetProperty("quoteId").GetGuid(), replacement.GetProperty("quoteId").GetGuid());
            Assert.Equal(T + TimeSpan.FromSeconds(31), replacement.GetProperty("issuedAt").GetDateTimeOffset());
            Assert.Equal(wallet, await fixture.AvailableAsync(fixture.CustomerId));
            fixture.LedgerClock.Set(T + TimeSpan.FromSeconds(32));
            await fixture.AwaitBuyAsync(
                await fixture.ConfirmedOrderAsync(replacement.GetProperty("quoteId").GetGuid()), "Completed");
            Assert.Equal(wallet - 1_010_000, await fixture.AvailableAsync(fixture.CustomerId));
        }
        finally
        {
            fixture.ResetClocks();
        }
    }

    [Fact]
    public async Task ABlockedFirstDecisionThatCrossesExpiryIsRejected()
    {
        await fixture.FundAsync(2_000_000, fixture.Other);
        try
        {
            fixture.ApplicationClock.Set(T);
            fixture.LedgerClock.Set(T);
            fixture.ObservePrice();
            var quote = await fixture.QuoteAsync(1_000_000, fixture.Other);
            var wallet = await fixture.AvailableAsync(fixture.OtherId);
            fixture.BlockOperations(true);
            var order = await fixture.ConfirmedOrderAsync(quote.GetProperty("quoteId").GetGuid(), customer: fixture.Other);
            fixture.LedgerClock.Set(T + TimeSpan.FromSeconds(45));
            fixture.BlockOperations(false);
            var rejected = await fixture.AwaitBuyAsync(order, "Rejected", fixture.Other);
            Assert.Equal("QuoteExpired", rejected.GetProperty("reasonCode").GetString());
            Assert.Equal(wallet, await fixture.AvailableAsync(fixture.OtherId));
        }
        finally
        {
            fixture.BlockOperations(false);
            fixture.ResetClocks();
        }
    }

    [Fact]
    public async Task AnIssuedQuoteStaysConfirmableAfterTheFeedFails()
    {
        await fixture.FundAsync(2_000_000);
        fixture.ObservePrice();
        var quote = await fixture.QuoteAsync(1_000_000);
        fixture.SetPrice(10_000_000, 9_800_000, null);
        using (var unavailable = await fixture.Customer.PostAsJsonAsync("/api/v1/gold/buy-quotes",
            new { requestedPrincipalIrr = 1_000_000 }))
            Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        await fixture.AwaitBuyAsync(await fixture.ConfirmedOrderAsync(quote.GetProperty("quoteId").GetGuid()), "Completed");
    }
}
