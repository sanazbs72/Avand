using System.Net;
using System.Text.Json;
using Avand.V01.IntegrationTests.Fixtures;
using Xunit;

namespace Avand.V01.IntegrationTests;

/// <summary>T021 / AC-12: identical concurrent confirmations, key/content conflicts and quote reuse.</summary>
public sealed class V03IdempotencyTests(V03Fixture fixture) : IClassFixture<V03Fixture>
{
    [Fact]
    public async Task HundredIdenticalConfirmationsProduceOneSaleAndOneJournal()
    {
        await fixture.GiveGoldAsync(fixture.CustomerId, 1000);
        await fixture.SetFreeLiquidityAsync(50_000_000);
        var gold = await fixture.GoldMgAsync(fixture.CustomerId);
        var wallet = await fixture.AvailableAsync(fixture.CustomerId);
        var journals = await fixture.JournalCountAsync();
        fixture.ObserveSellPrice();
        var quote = await fixture.SellQuoteAsync(quantityMg: 500);
        var quoteId = quote.GetProperty("quoteId").GetGuid();

        var replies = await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => fixture.ConfirmSellAsync(quoteId, "burst")));
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
        await fixture.AwaitSellAsync(orderId, "Completed");
        Assert.Equal(1, await fixture.ReadApplicationAsync("""SELECT count(*) FROM "SellOrders" WHERE "QuoteId" = $1""", quoteId));
        Assert.Equal(1, await fixture.SellCountAsync("\"QuoteId\" = $1", quoteId));
        Assert.Equal(1, await fixture.PostedSellCountAsync(orderId));
        Assert.Equal(journals + 1, await fixture.JournalCountAsync());
        Assert.Equal(gold - 500, await fixture.GoldMgAsync(fixture.CustomerId));
        Assert.Equal(wallet + 4_875_500, await fixture.AvailableAsync(fixture.CustomerId));

        // Exact retry after completion still returns the original acknowledgement.
        using (var retry = await fixture.ConfirmSellAsync(quoteId, "burst"))
            Assert.Equal(receipts[0], await retry.Content.ReadAsStringAsync());

        // Same key with another quote conflicts; another key cannot reuse the consumed quote.
        var other = await fixture.SellQuoteAsync(quantityMg: 500);
        using (var changed = await fixture.ConfirmSellAsync(other.GetProperty("quoteId").GetGuid(), "burst"))
            Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        using (var reused = await fixture.ConfirmSellAsync(quoteId, "another-key"))
            Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);
        Assert.Equal(1, await fixture.ReadApplicationAsync("""SELECT count(*) FROM "SellOrders" WHERE "CustomerId" = $1""",
            fixture.CustomerId));
        Assert.Equal(gold - 500, await fixture.GoldMgAsync(fixture.CustomerId));
        Assert.Equal(wallet + 4_875_500, await fixture.AvailableAsync(fixture.CustomerId));
    }
}
