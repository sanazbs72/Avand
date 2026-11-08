using System.Net;
using System.Net.Http.Json;
using Avand.V01.IntegrationTests.Fixtures;
using Xunit;

namespace Avand.V01.IntegrationTests;

public sealed class V02LedgerScopeTests(V02Fixture fixture) : IClassFixture<V02Fixture>
{
    [Fact]
    public async Task PaymentsAndTradingCannotUseEachOthersOperations()
    {
        using var trading = fixture.TradingClient();
        var customer = fixture.CustomerId;
        var payment = Guid.NewGuid();
        var operation = Guid.NewGuid();
        var capture = fixture.Capture(customer, payment, operation, 100);
        using var foreignPost = await trading.PostAsJsonAsync("/internal/v1/operations", capture);
        Assert.Equal(HttpStatusCode.Forbidden, foreignPost.StatusCode);
        using var foreignBuy = await fixture.Ledger.PostAsJsonAsync("/internal/v1/operations", new { operationType = "BuyGold" });
        Assert.Equal(HttpStatusCode.Forbidden, foreignBuy.StatusCode);

        await fixture.AuthorizeAsync(customer, payment);
        using var posted = await fixture.Ledger.PostAsJsonAsync("/internal/v1/operations", capture);
        posted.EnsureSuccessStatusCode();
        using var paymentInquiry = await fixture.Ledger.GetAsync($"/internal/v1/operations/{operation}");
        paymentInquiry.EnsureSuccessStatusCode();
        using var foreignInquiry = await trading.GetAsync($"/internal/v1/operations/{operation}");
        Assert.Equal(HttpStatusCode.NotFound, foreignInquiry.StatusCode);
        using var paymentBalance = await trading.GetAsync($"/internal/v1/customers/{customer}/balances");
        Assert.Equal(HttpStatusCode.Forbidden, paymentBalance.StatusCode);
    }
}
