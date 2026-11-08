using System.Net;
using System.Net.Http.Json;
using Avand.V01.IntegrationTests.Fixtures;
using Xunit;

namespace Avand.V01.IntegrationTests;

/// <summary>T008: SellGold is a Trading-only operation behind the shared internal route.</summary>
public sealed class V03LedgerScopeTests(V03Fixture fixture) : IClassFixture<V03Fixture>
{
    private object Envelope(string side = "Sell") => V03Fixture.SellEnvelope(fixture.CustomerId, Guid.NewGuid(),
        Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, 500, 4_900_000, 24_500, 4_875_500, side: side);

    [Fact]
    public async Task PaymentsAndAnonymousCallersCannotSendSellGold()
    {
        var journals = await fixture.JournalCountAsync();
        using var payments = await fixture.Ledger.PostAsJsonAsync("/internal/v1/operations", Envelope());
        Assert.Equal(HttpStatusCode.Forbidden, payments.StatusCode);
        using var anonymous = await fixture.Untrusted.PostAsJsonAsync("/internal/v1/operations", Envelope());
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(journals, await fixture.JournalCountAsync());
    }

    [Fact]
    public async Task TradingSellEnvelopeIsStrict()
    {
        using var trading = fixture.TradingClient();
        using var wrongSide = await trading.PostAsJsonAsync("/internal/v1/operations", Envelope(side: "Buy"));
        Assert.Equal(HttpStatusCode.BadRequest, wrongSide.StatusCode);
        using var unknownField = await trading.PostAsJsonAsync("/internal/v1/operations",
            new { operationType = "SellGold", schemaVersion = 1, unexpected = true });
        Assert.Equal(HttpStatusCode.BadRequest, unknownField.StatusCode);
    }

    [Fact]
    public async Task InquiryIsProducerScoped()
    {
        using var trading = fixture.TradingClient();
        var unknown = Guid.NewGuid();
        using var tradingInquiry = await trading.GetAsync($"/internal/v1/operations/{unknown}");
        Assert.Equal(HttpStatusCode.NotFound, tradingInquiry.StatusCode);
        using var paymentsInquiry = await fixture.Ledger.GetAsync($"/internal/v1/operations/{unknown}");
        Assert.Equal(HttpStatusCode.NotFound, paymentsInquiry.StatusCode);
    }
}
