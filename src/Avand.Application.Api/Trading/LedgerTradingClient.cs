using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace Avand.Application.Api.Trading;

internal sealed record LedgerTradingState(long AvailableIrr, long AvailableGoldMg, bool Eligible, LedgerBackingView Backing);
internal sealed record LedgerBackingView(string Status, long? HeadroomMg);
internal sealed record LedgerBuyResult(Guid OperationId, string Status, Guid? JournalId, DateTimeOffset DecidedAt,
    LedgerBuyOutcome? Decision);
internal sealed record LedgerBuyOutcome(string? RejectionCode, long IncrementalUncoveredGoldMg, Guid? OriginalOperationId);
internal sealed record LedgerSellResult(Guid OperationId, string Status, Guid? JournalId, DateTimeOffset DecidedAt,
    LedgerSellOutcome? Decision);
internal sealed record LedgerSellOutcome(string? RejectionCode, Guid? OriginalOperationId);

/// <summary>Application.Trading access to the Ledger. Uses its own credential with no fallback to Payments.</summary>
internal sealed class LedgerTradingClient(HttpClient client)
{
    /// <summary>Resolves an order under its original operation ID: inquiry first, then the stored frozen request.
    /// Returns null when the outcome stays unknown (transport failure, ordinary NotFound after send, or any other status).</summary>
    public async Task<LedgerBuyResult?> DeliverAsync(Guid operationId, string frozenRequest, CancellationToken ct)
    {
        var result = await DeliverAsync<LedgerBuyResult>(operationId, frozenRequest, ct);
        return result?.OperationId == operationId ? result : null;
    }

    /// <summary>SellGold delivery with the same inquiry-first, original-identity semantics as buys.</summary>
    public async Task<LedgerSellResult?> DeliverSellAsync(Guid operationId, string frozenRequest, CancellationToken ct)
    {
        var result = await DeliverAsync<LedgerSellResult>(operationId, frozenRequest, ct);
        return result?.OperationId == operationId ? result : null;
    }

    private async Task<T?> DeliverAsync<T>(Guid operationId, string frozenRequest, CancellationToken ct) where T : class
    {
        using var lookup = await client.GetAsync($"internal/v1/operations/{operationId}", ct);
        if (lookup.IsSuccessStatusCode)
            return await lookup.Content.ReadFromJsonAsync<T>(ct);
        if (lookup.StatusCode != HttpStatusCode.NotFound)
            return null;

        using var content = new StringContent(frozenRequest, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("internal/v1/operations", content, ct);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<T>(ct) : null;
    }

    /// <summary>Advisory committed state. Null means unavailable; it never becomes a fabricated zero.</summary>
    public async Task<LedgerTradingState?> FinancialStateAsync(Guid customer, CancellationToken ct)
    {
        try
        {
            using var response = await client.GetAsync($"internal/v1/customers/{customer}/financial-state", ct);
            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<LedgerTradingState>(ct)
                : null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return null;
        }
    }
}
