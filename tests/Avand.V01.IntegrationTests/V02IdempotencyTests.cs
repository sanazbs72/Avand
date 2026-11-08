using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Avand.V01.IntegrationTests.Fixtures;
using Xunit;

namespace Avand.V01.IntegrationTests;

/// <summary>AC-13: identical concurrent confirmations, key/content conflicts and quote reuse.</summary>
public sealed class V02IdempotencyTests(V02Fixture fixture) : IClassFixture<V02Fixture>
{
    [Fact]
    public async Task HundredIdenticalConfirmationsProduceOneOrderAndOneFinancialEffect()
    {
        await fixture.FundAsync(3_000_000);
        var wallet = await fixture.AvailableAsync(fixture.CustomerId);
        fixture.ObservePrice();
        var quote = await fixture.QuoteAsync(1_000_000);
        var quoteId = quote.GetProperty("quoteId").GetGuid();

        var replies = await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => fixture.ConfirmAsync(quoteId, "burst")));
        string[] receipts;
        try
        {
            Assert.All(replies, reply => Assert.Equal(HttpStatusCode.Accepted, reply.StatusCode));
            receipts = await Task.WhenAll(replies.Select(reply => reply.Content.ReadAsStringAsync()));
        }
        finally
        {
            foreach (var reply in replies) reply.Dispose();
        }

        Assert.Single(receipts.Distinct());
        var orderId = JsonDocument.Parse(receipts[0]).RootElement.GetProperty("orderId").GetGuid();
        await fixture.AwaitBuyAsync(orderId, "Completed");

        Assert.Equal(1, await fixture.ReadApplicationAsync("""SELECT count(*) FROM "BuyOrders" WHERE "QuoteId" = $1""", quoteId));
        Assert.Equal(1, await fixture.ReadLedgerAsync("""SELECT count(*) FROM "BuyDecisions" WHERE "QuoteId" = $1""", quoteId));
        Assert.Equal(1, await fixture.PostedBuyCountAsync(orderId));
        Assert.Equal(wallet - 1_010_000, await fixture.AvailableAsync(fixture.CustomerId));
        Assert.Equal(100, (await fixture.FinancialStateAsync(fixture.CustomerId)).GetProperty("availableGoldMg").GetInt64());

        // Exact retry after completion still returns the original acknowledgement.
        using (var retry = await fixture.ConfirmAsync(quoteId, "burst"))
            Assert.Equal(receipts[0], await retry.Content.ReadAsStringAsync());

        // Same key with other content conflicts; a different key cannot reuse the consumed quote.
        var other = await fixture.QuoteAsync(1_000_000);
        using (var changed = await fixture.ConfirmAsync(other.GetProperty("quoteId").GetGuid(), "burst"))
            Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        using (var reused = await fixture.ConfirmAsync(quoteId, "another-key"))
            Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);
        Assert.Equal(1, await fixture.ReadApplicationAsync("""SELECT count(*) FROM "BuyOrders" WHERE "CustomerId" = $1""",
            fixture.CustomerId));
        Assert.Equal(wallet - 1_010_000, await fixture.AvailableAsync(fixture.CustomerId));
    }

    /// <summary>A transient database failure during admission (here 53300, no connection slot for the role) is a
    /// retryable 503, never a 500, and the exact retry with the same key later admits exactly one order.</summary>
    [Fact]
    public async Task ExhaustedDatabaseConnectionsReturnRetryableUnavailableAndTheSameKeyLaterAdmits()
    {
        fixture.ObservePrice();
        var quote = await fixture.QuoteAsync(1_000_000, fixture.Other);
        var quoteId = quote.GetProperty("quoteId").GetGuid();

        await using (await fixture.RefuseApplicationConnectionsAsync())
        {
            using var starved = await fixture.ConfirmAsync(quoteId, "starved", fixture.Other);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, starved.StatusCode);
            var body = await starved.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("OrderAdmissionUnavailable", body.GetProperty("code").GetString());
            Assert.True(body.GetProperty("retryable").GetBoolean());
        }
        Assert.Equal(0, await fixture.ReadApplicationAsync("""SELECT count(*) FROM "BuyOrders" WHERE "QuoteId" = $1""", quoteId));

        var orderId = await fixture.ConfirmedOrderAsync(quoteId, "starved", fixture.Other);
        Assert.Equal(orderId, await fixture.ConfirmedOrderAsync(quoteId, "starved", fixture.Other));
        Assert.Equal(1, await fixture.ReadApplicationAsync("""SELECT count(*) FROM "BuyOrders" WHERE "QuoteId" = $1""", quoteId));
        // Other is never funded enough for this order here, whichever test in this class runs first.
        await fixture.AwaitBuyAsync(orderId, "Rejected", fixture.Other);
    }

    [Fact]
    public async Task LedgerReplaysTheStoredDecisionAndRejectsConflictingIdentities()
    {
        await fixture.FundAsync(2_000_000, fixture.Other);
        fixture.ObservePrice();
        var quote = await fixture.QuoteAsync(1_000_000, fixture.Other);
        var orderId = await fixture.ConfirmedOrderAsync(quote.GetProperty("quoteId").GetGuid(), customer: fixture.Other);
        var completed = await fixture.AwaitBuyAsync(orderId, "Completed", fixture.Other);
        var wallet = await fixture.AvailableAsync(fixture.OtherId);
        var journals = await fixture.JournalCountAsync();

        using var trading = fixture.TradingClient();
        var frozen = await fixture.FrozenRequestAsync(orderId);
        var operationId = JsonDocument.Parse(frozen).RootElement.GetProperty("operationId").GetGuid();
        using (var replay = await trading.PostAsync("/internal/v1/operations", new StringContent(frozen, Encoding.UTF8, "application/json")))
        {
            var result = await replay.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Posted", result.GetProperty("status").GetString());
            Assert.Equal(completed.GetProperty("decidedAt").GetDateTimeOffset(), result.GetProperty("decidedAt").GetDateTimeOffset());
        }

        // A rebuilt identical envelope is a replay; changed economics under the same operation ID conflict.
        var rebuilt = await fixture.SubmitBuyAsync(V02Fixture.BuyEnvelope(quote, fixture.OtherId, orderId, operationId));
        Assert.Equal("Posted", rebuilt.GetProperty("status").GetString());
        var changed = frozen.Replace("\"quantityMg\":100", "\"quantityMg\":99", StringComparison.Ordinal);
        Assert.NotEqual(frozen, changed);
        using (var conflict = await trading.PostAsync("/internal/v1/operations",
            new StringContent(changed, Encoding.UTF8, "application/json")))
            Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

        var duplicate = await fixture.SubmitBuyAsync(V02Fixture.BuyEnvelope(quote, fixture.OtherId, orderId, Guid.NewGuid()));
        Assert.Equal("Rejected", duplicate.GetProperty("status").GetString());
        Assert.Equal("DuplicateBusinessAction", duplicate.GetProperty("decision").GetProperty("rejectionCode").GetString());
        Assert.Equal(operationId, duplicate.GetProperty("decision").GetProperty("originalOperationId").GetGuid());
        var reused = await fixture.SubmitBuyAsync(V02Fixture.BuyEnvelope(quote, fixture.OtherId, Guid.NewGuid(), Guid.NewGuid()));
        Assert.Equal("QuoteAlreadyUsed", reused.GetProperty("decision").GetProperty("rejectionCode").GetString());

        Assert.Equal(journals, await fixture.JournalCountAsync());
        Assert.Equal(wallet, await fixture.AvailableAsync(fixture.OtherId));
    }
}
