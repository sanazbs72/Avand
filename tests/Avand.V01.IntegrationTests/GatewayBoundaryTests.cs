using System.Net.Http.Json;
using System.Text.Json;
using Avand.V01.IntegrationTests.Fixtures;
using Xunit;

namespace Avand.V01.IntegrationTests;

public sealed class GatewayBoundaryTests
{
    [Theory]
    [InlineData(-1, "Captured")]
    [InlineData(0, "Captured")]
    [InlineData(1, "Expired")]
    public async Task FirstVerifyUsesInclusiveSessionBoundary(int millisecondsFromBoundary, string expected)
    {
        await using var gateway = new MockGateway();
        await gateway.StartAsync();
        using var client = new HttpClient { BaseAddress = gateway.BaseAddress };
        var paymentId = Guid.NewGuid();
        using var session = await client.PostAsJsonAsync("/sessions", new { requestReference = paymentId, amountIrr = 100 });
        session.EnsureSuccessStatusCode();
        gateway.SetResult(paymentId, "Ready");
        gateway.Advance(TimeSpan.FromMinutes(15) + TimeSpan.FromMilliseconds(millisecondsFromBoundary));

        using var verification = await client.PostAsync($"/payments/{paymentId}/verify", null);
        verification.EnsureSuccessStatusCode();
        var result = await verification.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(expected, result.GetProperty("outcome").GetString());
    }

    [Fact]
    public async Task InquiryReturnsPreviouslyVerifiedCaptureAfterWindow()
    {
        await using var gateway = new MockGateway();
        await gateway.StartAsync();
        using var client = new HttpClient { BaseAddress = gateway.BaseAddress };
        var paymentId = Guid.NewGuid();
        using var session = await client.PostAsJsonAsync("/sessions", new { requestReference = paymentId, amountIrr = 100 });
        session.EnsureSuccessStatusCode();
        gateway.SetResult(paymentId, "Ready");
        using var verification = await client.PostAsync($"/payments/{paymentId}/verify", null);
        verification.EnsureSuccessStatusCode();
        gateway.Advance(TimeSpan.FromMinutes(20));

        var result = await client.GetFromJsonAsync<JsonElement>($"/payments/{paymentId}");
        Assert.Equal("Captured", result.GetProperty("outcome").GetString());
        Assert.Equal(paymentId, result.GetProperty("paymentId").GetGuid());
        Assert.Equal(100L, result.GetProperty("amountIrr").GetInt64());
        Assert.False(result.TryGetProperty("verifiedAt", out _));
    }
}
