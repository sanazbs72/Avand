using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Avand.V01.IntegrationTests.Fixtures;
using Xunit;

namespace Avand.V01.IntegrationTests;

/// <summary>T009: customer sell contract (contracts/v03.md) — AC-01, AC-01b, AC-04, AC-05.</summary>
public sealed class V03CustomerContractTests(V03Fixture fixture) : IClassFixture<V03Fixture>
{
    [Fact]
    public async Task WeightAndGrossQuotesShowSellPriceFeeAndNet()
    {
        fixture.ObserveSellPrice();
        foreach (var (quote, mode) in new[]
        {
            (await fixture.SellQuoteAsync(quantityMg: 500), "Weight"),
            (await fixture.SellQuoteAsync(requestedGrossIrr: 4_900_001), "GrossIrr")
        })
        {
            Assert.Equal("Sell", quote.GetProperty("side").GetString());
            Assert.Equal(mode, quote.GetProperty("inputMode").GetString());
            Assert.Equal(9_800_000, quote.GetProperty("priceIrrPerGram").GetInt64());
            Assert.Equal(500, quote.GetProperty("quantityMg").GetInt64());
            Assert.Equal(4_900_000, quote.GetProperty("grossIrr").GetInt64());
            Assert.Equal(5000, quote.GetProperty("feeRatePpm").GetInt32());
            Assert.Equal(24_500, quote.GetProperty("feeIrr").GetInt64());
            Assert.Equal(4_875_500, quote.GetProperty("netIrr").GetInt64());
            Assert.Equal(TimeSpan.FromSeconds(30),
                quote.GetProperty("expiresAt").GetDateTimeOffset() - quote.GetProperty("issuedAt").GetDateTimeOffset());
        }
    }

    [Theory]
    [InlineData(1000, 1_000_001, 1_000_001, 5001, 995_000)]
    [InlineData(101, 9_900_999, 1_000_000, 5000, 995_000)]
    public async Task RoundingFollowsC02(long weight, long bid, long gross, long fee, long net)
    {
        fixture.ObservePrice(bid, bid);
        var quote = await fixture.SellQuoteAsync(quantityMg: weight);
        Assert.Equal(gross, quote.GetProperty("grossIrr").GetInt64());
        Assert.Equal(fee, quote.GetProperty("feeIrr").GetInt64());
        Assert.Equal(net, quote.GetProperty("netIrr").GetInt64());
    }

    [Fact]
    public async Task ConfiguredRateChangesTheFee()
    {
        fixture.ObserveSellPrice();
        fixture.SetSellRate(10_000);
        try
        {
            var quote = await fixture.SellQuoteAsync(quantityMg: 500);
            Assert.Equal(49_000, quote.GetProperty("feeIrr").GetInt64());
            Assert.Equal(4_851_000, quote.GetProperty("netIrr").GetInt64());
        }
        finally
        {
            fixture.SetSellRate(null);
        }
    }

    [Theory]
    [InlineData(100L, null, 9_999_999L, "OrderOutOfBounds")]           // gross 999,999
    [InlineData(null, 1_000_000L, 9_999_999L, "OrderOutOfBounds")]     // rial input rounds to gross 999,999
    [InlineData(500_001L, null, 10_000_000L, "OrderOutOfBounds")]      // gross 5,000,010,000
    [InlineData(0L, null, 9_800_000L, "InvalidAmount")]
    [InlineData(null, -5L, 9_800_000L, "InvalidAmount")]
    [InlineData(null, 9_799L, 9_800_000L, "QuantityBelowOneMilligram")]
    public async Task InvalidOrOutOfBoundsAmountsAreRejected(long? weight, long? gross, long bid, string code)
    {
        fixture.ObservePrice(Math.Max(bid, 10_000_000), bid);
        using var response = await fixture.RequestSellQuoteAsync(new { quantityMg = weight, requestedGrossIrr = gross });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(code, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task GrossExactlyAtTheCeilingIsAllowed()
    {
        fixture.ObservePrice(10_000_000, 10_000_000);
        var quote = await fixture.SellQuoteAsync(quantityMg: 500_000);
        Assert.Equal(5_000_000_000, quote.GetProperty("grossIrr").GetInt64());
    }

    [Fact]
    public async Task QuoteBodyMustNameExactlyOneAmount()
    {
        fixture.ObserveSellPrice();
        foreach (var body in new[] { "{}", """{"quantityMg":500,"requestedGrossIrr":4900000}""", """{"quantityMg":500,"extra":1}""" })
        {
            using var response = await fixture.Customer.PostAsync("/api/v1/gold/sell-quotes",
                new StringContent(body, Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task StalePriceGivesNoQuote()
    {
        fixture.SetPrice(10_000_000, 9_800_000, V02Fixture.Iso(DateTimeOffset.UtcNow.AddSeconds(-31)));
        using var response = await fixture.RequestSellQuoteAsync(new { quantityMg = 500 });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("PriceUnavailable",
            (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task IneligibleCustomerCannotQuote()
    {
        fixture.ObserveSellPrice();
        using var response = await fixture.RequestSellQuoteAsync(new { quantityMg = 500 }, fixture.Restricted);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ConfirmationIsStrictAndOrdersAreOwnerScoped()
    {
        fixture.ObserveSellPrice();
        var quote = await fixture.SellQuoteAsync(quantityMg: 500);
        var quoteId = quote.GetProperty("quoteId").GetGuid();

        using var extra = await fixture.Customer.PostAsync("/api/v1/gold/sell-orders",
            new StringContent($$"""{"quoteId":"{{quoteId}}","netIrr":1}""", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, extra.StatusCode);
        using var foreignConfirm = await fixture.ConfirmSellAsync(quoteId, "foreign", fixture.Other);
        Assert.Equal(HttpStatusCode.NotFound, foreignConfirm.StatusCode);

        using var accepted = await fixture.ConfirmSellAsync(quoteId, "contract");
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        var receipt = await accepted.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = receipt.GetProperty("orderId").GetGuid();
        Assert.Equal($"/api/v1/gold/sell-orders/{orderId:D}", receipt.GetProperty("statusLocation").GetString());

        using var owned = await fixture.Customer.GetAsync($"/api/v1/gold/sell-orders/{orderId}");
        Assert.Equal(HttpStatusCode.OK, owned.StatusCode);
        using var foreignRead = await fixture.Other.GetAsync($"/api/v1/gold/sell-orders/{orderId}");
        Assert.Equal(HttpStatusCode.NotFound, foreignRead.StatusCode);
    }
}
