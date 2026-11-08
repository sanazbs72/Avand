using System.Globalization;
using System.Net;
using System.Net.Http.Json;

namespace Avand.Application.Api.Payments;

internal sealed record LedgerCapture(Guid OperationId, Guid TopUpId, Guid CustomerId, Guid PaymentId,
    string CaptureReference, long AmountIrr, Guid EvidenceId, string Digest);
internal sealed record LedgerControl(Guid CommandId, string Status);
internal sealed record LedgerDecision(Guid OperationId, string Status, Guid? JournalId, LedgerDetail? Decision);
internal sealed record LedgerDetail(string? CreditedBucket, string? RejectionCode);
internal sealed record WalletBalance(long AvailableIrr);

internal sealed class LedgerPaymentsClient(HttpClient client)
{
    public async Task<LedgerControl?> AuthorizeAsync(Guid intent, Guid customer, CancellationToken ct)
    {
        using var response = await client.PostAsJsonAsync("internal/v1/control-commands", new
        {
            schemaVersion = 1,
            commandId = intent,
            commandType = "AuthorizeTopUpInitiation",
            customerId = customer,
            payload = new { paymentIntentId = intent }
        }, ct);
        if (!response.IsSuccessStatusCode)
            return null;

        var result = await response.Content.ReadFromJsonAsync<LedgerControl>(ct);
        return result?.CommandId == intent ? result : null;
    }

    public async Task<LedgerDecision?> DeliverAsync(LedgerCapture capture, CancellationToken ct)
    {
        using var lookup = await client.GetAsync($"internal/v1/operations/{capture.OperationId}", ct);
        if (lookup.IsSuccessStatusCode)
            return await ReadDecisionAsync(lookup, capture.OperationId, ct);
        if (lookup.StatusCode != HttpStatusCode.NotFound)
            return null;

        using var response = await client.PostAsJsonAsync("internal/v1/operations", new
        {
            schemaVersion = 1,
            operationId = capture.OperationId,
            operationType = "ApplyGatewayPaymentEvidence",
            businessReference = new { kind = "WalletTopUp", id = capture.TopUpId, action = "Capture" },
            customerId = capture.CustomerId,
            actor = new { kind = "Customer", id = capture.CustomerId },
            correlationId = capture.TopUpId,
            payload = new
            {
                paymentId = capture.PaymentId,
                captureRef = capture.CaptureReference,
                capturedIrr = capture.AmountIrr.ToString(CultureInfo.InvariantCulture),
                evidenceBatchId = capture.EvidenceId,
                confirmedReversals = Array.Empty<object>(),
                evidenceRef = new { id = capture.EvidenceId, digest = capture.Digest }
            }
        }, ct);

        return response.IsSuccessStatusCode
            ? await ReadDecisionAsync(response, capture.OperationId, ct)
            : null;
    }

    public async Task<WalletBalance?> BalanceAsync(Guid customer, CancellationToken ct)
    {
        using var response = await client.GetAsync($"internal/v1/customers/{customer}/balances", ct);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<WalletBalance>(ct) : null;
    }

    private static async Task<LedgerDecision?> ReadDecisionAsync(HttpResponseMessage response, Guid id, CancellationToken ct)
    {
        var result = await response.Content.ReadFromJsonAsync<LedgerDecision>(ct);
        return result?.OperationId == id ? result : null;
    }
}
