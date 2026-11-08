using System.Net.Http.Json;

namespace Avand.Application.Api.Payments;

internal sealed record GatewaySession(Guid PaymentId, DateTimeOffset CreatedAt, string Continuation);
internal sealed record GatewayResult(Guid PaymentId, string Outcome, string? CaptureReference, long? AmountIrr);

internal sealed class MockGatewayAdapter(HttpClient client)
{
    public async Task<GatewaySession?> CreateSessionAsync(Guid requestReference, long amountIrr, CancellationToken ct)
    {
        using var response = await client.PostAsJsonAsync("sessions", new { requestReference, amountIrr }, ct);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<GatewaySession>(ct) : null;
    }

    public async Task<GatewayResult?> ResolveAsync(Guid payment, bool allowVerify, CancellationToken ct)
    {
        using var inquiry = await client.GetAsync($"payments/{payment}", ct);
        if (!inquiry.IsSuccessStatusCode)
            return null;
        var result = await inquiry.Content.ReadFromJsonAsync<GatewayResult>(ct);
        if (!allowVerify || result is null || result.PaymentId != payment || result.Outcome != "Unknown")
            return result;

        using var verify = await client.PostAsync($"payments/{payment}/verify", null, ct);
        return verify.IsSuccessStatusCode ? await verify.Content.ReadFromJsonAsync<GatewayResult>(ct) : null;
    }
}
