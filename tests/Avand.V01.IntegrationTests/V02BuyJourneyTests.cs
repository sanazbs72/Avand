using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Avand.V01.IntegrationTests.Fixtures;
using Xunit;

namespace Avand.V01.IntegrationTests;

public sealed class V02BuyJourneyTests(V02Fixture fixture) : IClassFixture<V02Fixture>
{
    [Fact]
    public async Task TopUpFundsExactlyOneAtomicGoldPurchase()
    {
        await fixture.FundAsync(10_100_001);
        fixture.ObservePrice();
        var quote = await fixture.QuoteAsync(10_000_001);
        using var reply = await fixture.ConfirmAsync(quote.GetProperty("quoteId").GetGuid(), "happy");
        Assert.Equal(HttpStatusCode.Accepted, reply.StatusCode);
        var receipt = await reply.Content.ReadFromJsonAsync<JsonElement>();
        var order = await fixture.AwaitBuyAsync(receipt.GetProperty("orderId").GetGuid(), "Completed");
        Assert.Equal(100_000, order.GetProperty("feeIrr").GetInt64());

        var balances = await fixture.Customer.GetFromJsonAsync<JsonElement>("/api/v1/gold/balances");
        Assert.Equal(1, balances.GetProperty("availableIrr").GetInt64());
        Assert.Equal(1000, balances.GetProperty("availableGoldMg").GetInt64());
        Assert.Equal(1, await fixture.ReadLedgerAsync("""SELECT count(*) FROM "Journals" WHERE "Template" = 'F04'"""));
        Assert.Equal(0, await fixture.ReadLedgerAsync("""
            SELECT count(*) FROM (
                SELECT a."Asset" FROM "JournalLines" l JOIN "Accounts" a ON a."Id" = l."AccountId"
                JOIN "Journals" j ON j."Id" = l."JournalId" WHERE j."Template" = 'F04'
                GROUP BY a."Asset"
                HAVING sum(CASE WHEN l."IsDebit" THEN l."AmountMinor" ELSE -l."AmountMinor" END) <> 0
            ) AS imbalances
            """));
    }

    [Fact]
    public async Task RestrictionBeforeTheFirstLedgerDecisionBlocksThePurchase()
    {
        using (var ineligible = await fixture.Restricted.PostAsJsonAsync("/api/v1/gold/buy-quotes",
            new { requestedPrincipalIrr = 1_000_000 }))
            Assert.Equal(HttpStatusCode.Forbidden, ineligible.StatusCode);

        await fixture.SetEligibleAsync(fixture.RestrictedId);
        await fixture.FundAsync(2_000_000, fixture.Restricted);
        fixture.ObservePrice();
        var quote = await fixture.QuoteAsync(1_000_000, fixture.Restricted);
        fixture.BlockOperations(true);
        try
        {
            var order = await fixture.ConfirmedOrderAsync(quote.GetProperty("quoteId").GetGuid(), customer: fixture.Restricted);
            await fixture.RestrictAsync(fixture.RestrictedId);
            fixture.BlockOperations(false);
            var rejected = await fixture.AwaitBuyAsync(order, "Rejected", fixture.Restricted);
            Assert.Equal("CustomerNotEligible", rejected.GetProperty("reasonCode").GetString());
            Assert.Equal(0, await fixture.PostedBuyCountAsync(order));
            Assert.Equal(2_000_000, await fixture.AvailableAsync(fixture.RestrictedId));
        }
        finally
        {
            fixture.BlockOperations(false);
        }
    }

    [Fact]
    public async Task UnavailableBalancesAreNeverShownAsZero()
    {
        fixture.BlockOperations(true);
        try
        {
            using var reply = await fixture.Customer.GetAsync("/api/v1/gold/balances");
            Assert.Equal(HttpStatusCode.ServiceUnavailable, reply.StatusCode);
            var body = await reply.Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(body.TryGetProperty("availableIrr", out _));
        }
        finally
        {
            fixture.BlockOperations(false);
        }
    }

    [Fact]
    public async Task AWalletOneRialShortIsNeverResized()
    {
        await fixture.FundAsync(1_009_999, fixture.Other);
        fixture.ObservePrice();
        var quote = await fixture.QuoteAsync(1_000_000, fixture.Other);
        using var reply = await fixture.ConfirmAsync(quote.GetProperty("quoteId").GetGuid(), "short", fixture.Other);
        Assert.Equal(HttpStatusCode.Accepted, reply.StatusCode);
        var receipt = await reply.Content.ReadFromJsonAsync<JsonElement>();
        var rejected = await fixture.AwaitBuyAsync(receipt.GetProperty("orderId").GetGuid(), "Rejected", fixture.Other);
        Assert.Equal("InsufficientRialBalance", rejected.GetProperty("reasonCode").GetString());
        Assert.Equal(1_009_999, await fixture.AvailableAsync(fixture.OtherId));
        Assert.Equal(0, await fixture.ReadLedgerAsync("""
            SELECT count(*) FROM "BuyDecisions" WHERE "CustomerId" = $1 AND "JournalId" IS NOT NULL
            """, fixture.OtherId));
    }
}
