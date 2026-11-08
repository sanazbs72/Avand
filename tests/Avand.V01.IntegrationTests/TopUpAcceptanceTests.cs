using System.Net;
using System.Net.Http.Json;
using Avand.V01.IntegrationTests.Fixtures;
using Xunit;

namespace Avand.V01.IntegrationTests;

public sealed class TopUpAcceptanceTests(V01Fixture fixture) : IClassFixture<V01Fixture>
{
    [Fact]
    public async Task DuplicateCallbackAndAdmissionProduceOneOwnedCredit()
    {
        const string key = "duplicate";
        var ids = await Task.WhenAll(fixture.StartAsync(key: key), fixture.StartAsync(key: key));
        Assert.Equal(ids[0], ids[1]);
        var id = ids[0];
        await fixture.AwaitStatusAsync(id, "AwaitingProvider");
        Assert.Equal(0L, await fixture.AvailableAsync(fixture.CustomerId));
        using var denied = await fixture.Other.GetAsync($"/api/v1/wallet/top-ups/{id}");
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        fixture.Gateway.SetResult(id, "Ready");
        await Task.WhenAll(fixture.NotifyAsync(id), fixture.NotifyAsync(id), fixture.NotifyAsync(id));
        await fixture.AwaitStatusAsync(id, "Succeeded");

        Assert.Equal(100L, await fixture.AvailableAsync(fixture.CustomerId));
        Assert.Equal(0L, await fixture.AvailableAsync(fixture.OtherId));
        Assert.Equal(1L, await fixture.PostingCountAsync(id));
        await fixture.AssertRoleIsolationAsync();
    }

    [Fact]
    public async Task IneligibleCustomerNeverObtainsSession()
    {
        using var unauthorized = await fixture.Callback.PostAsJsonAsync("/api/v1/wallet/top-ups", new { amountIrr = 100 });
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        var id = await fixture.StartAsync(fixture.Restricted);
        var result = await fixture.AwaitStatusAsync(id, "InitiationRejected", fixture.Restricted);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, result.GetProperty("continuation").ValueKind);
        Assert.Equal(0L, await fixture.DecisionCountAsync(id));
    }
}
