using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Avand.V01.IntegrationTests.Fixtures;
using Xunit;

namespace Avand.V01.IntegrationTests;

public sealed class V02CustomerContractTests(V02Fixture fixture) : IClassFixture<V02Fixture>
{
    [Fact]
    public async Task QuoteShowsBuySideAndExactRoundedTerms()
    {
        fixture.ObservePrice(10_000_019);
        var quote = await fixture.QuoteAsync(1_000_002);
        Assert.Equal(10_000_019, quote.GetProperty("priceIrrPerGram").GetInt64());
        Assert.Equal(100, quote.GetProperty("quantityMg").GetInt64());
        Assert.Equal(1_000_001, quote.GetProperty("principalIrr").GetInt64());
        Assert.Equal(10_001, quote.GetProperty("feeIrr").GetInt64());
        Assert.Equal(1_010_002, quote.GetProperty("totalDebitIrr").GetInt64());

        fixture.SetBuyRate(5000);
        try
        {
            quote = await fixture.QuoteAsync(1_000_002);
            Assert.Equal(5001, quote.GetProperty("feeIrr").GetInt64());
        }
        finally
        {
            fixture.SetBuyRate(null);
        }
    }

    [Fact]
    public async Task FinalPrincipalLimitsAndOwnershipAreEnforced()
    {
        fixture.ObservePrice(1000, 999);
        foreach (var limit in new long[] { 1_000_000, 5_000_000_000 })
            Assert.Equal(limit, (await fixture.QuoteAsync(limit)).GetProperty("principalIrr").GetInt64());
        foreach (var outside in new long[] { 0, 999_999, 5_000_000_001 })
        {
            using var reply = await fixture.Customer.PostAsJsonAsync("/api/v1/gold/buy-quotes",
                new { requestedPrincipalIrr = outside });
            Assert.Equal(HttpStatusCode.BadRequest, reply.StatusCode);
        }
        fixture.ObservePrice(9_999_999);
        using var belowAfterRounding = await fixture.Customer.PostAsJsonAsync("/api/v1/gold/buy-quotes",
            new { requestedPrincipalIrr = 1_000_000 });
        Assert.Equal(HttpStatusCode.BadRequest, belowAfterRounding.StatusCode);

        fixture.ObservePrice();
        var quote = await fixture.QuoteAsync(1_000_000);
        var quoteId = quote.GetProperty("quoteId").GetGuid();
        using var foreign = await fixture.ConfirmAsync(quoteId, "foreign", fixture.Other);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        using var wrongCredential = await fixture.ConfirmAsync(quoteId, "callback", fixture.Callback);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongCredential.StatusCode);
        using var admitted = await fixture.ConfirmAsync(quoteId, "owner");
        Assert.Equal(HttpStatusCode.Accepted, admitted.StatusCode);
        var receipt = await admitted.Content.ReadFromJsonAsync<JsonElement>();
        var location = receipt.GetProperty("statusLocation").GetString();
        Assert.Equal(location, admitted.Headers.Location?.OriginalString);
        using var foreignStatus = await fixture.Other.GetAsync(location);
        Assert.Equal(HttpStatusCode.NotFound, foreignStatus.StatusCode);
        using var unauthorizedBalance = await fixture.Callback.GetAsync("/api/v1/gold/balances");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorizedBalance.StatusCode);
    }
}
