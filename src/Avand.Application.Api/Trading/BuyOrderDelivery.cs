using Avand.Application.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Avand.Application.Api.Trading;

internal sealed record BuyCompletion(bool IsFinal, Func<ApplicationDbContext, CancellationToken, Task>? Apply = null);

/// <summary>Sends the stored frozen request under the original operation ID and applies a definitive Ledger result.</summary>
internal sealed class BuyOrderDelivery(ApplicationDbContext db, LedgerTradingClient ledger, TimeProvider clock,
    ILogger<BuyOrderDelivery> logger)
{
    internal static bool IsTerminal(string status) => status is "Completed" or "Rejected";

    public async Task<BuyCompletion> ProcessAsync(Guid orderId, CancellationToken ct)
    {
        var order = await db.BuyOrders.AsNoTracking().SingleAsync(x => x.Id == orderId, ct);
        if (IsTerminal(order.Status))
            return new BuyCompletion(true);

        var quantity = await db.BuyQuotes.AsNoTracking().Where(x => x.Id == order.QuoteId)
            .Select(x => x.QuantityMg).SingleAsync(ct);
        LedgerBuyResult? result;
        try
        {
            result = await ledger.DeliverAsync(order.OperationId, order.Request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            // Transport ambiguity: the Ledger may have committed. Keep the order unresolved and recover by identity.
            logger.LogWarning("Buy order {OrderId}, operation {OperationId}: Ledger outcome unknown ({ErrorType})",
                order.Id, order.OperationId, ex.GetType().Name);
            result = null;
        }

        if (result is null || !IsDefinitive(result, quantity))
            return new BuyCompletion(false, (store, token) => MarkUnknownAsync(store, order.Id, token));
        return new BuyCompletion(true, (store, token) => ApplyAsync(store, order.Id, result, token));
    }

    /// <summary>Applies the original definitive outcome once. Order state and any positive procurement intent from the
    /// Ledger's stored increment commit together. A repeated application leaves both unchanged.</summary>
    internal async Task ApplyAsync(ApplicationDbContext store, Guid orderId, LedgerBuyResult result, CancellationToken ct)
    {
        var rows = await store.BuyOrders.FromSqlInterpolated($"""
            SELECT * FROM "BuyOrders" WHERE "Id" = {orderId} FOR UPDATE
            """).ToListAsync(ct);
        var order = rows.Single();
        if (IsTerminal(order.Status))
            return;

        var posted = result.Status == "Posted";
        var increment = result.Decision?.IncrementalUncoveredGoldMg ?? 0;
        order.Status = posted ? "Completed" : "Rejected";
        order.ReasonCode = posted ? null : result.Decision?.RejectionCode;
        order.JournalId = result.JournalId;
        order.DecidedAt = result.DecidedAt;
        order.IncrementalUncoveredGoldMg = posted ? increment : 0;
        order.Version++;
        if (posted && increment > 0 && !await store.ProcurementIntents.AnyAsync(x => x.OrderId == orderId, ct))
        {
            store.Add(new ProcurementIntent
            {
                Id = Guid.NewGuid(),
                OrderId = orderId,
                OperationId = order.OperationId,
                JournalId = result.JournalId!.Value,
                UncoveredGoldMg = increment,
                CreatedAt = clock.GetUtcNow()
            });
        }

        logger.LogInformation(
            "Buy order {OrderId}, operation {OperationId}: applied {Status}, journal {JournalId}, reason {Reason}, procurement {IncrementMg} mg",
            orderId, order.OperationId, result.Status, result.JournalId, order.ReasonCode, order.IncrementalUncoveredGoldMg);
    }

    private static async Task MarkUnknownAsync(ApplicationDbContext store, Guid orderId, CancellationToken ct)
    {
        var order = await store.BuyOrders.SingleAsync(x => x.Id == orderId, ct);
        if (order.Status != "Processing")
            return;
        order.Status = "FinancialOutcomeUnknown";
        order.Version++;
    }

    private static bool IsDefinitive(LedgerBuyResult result, long quantity) => result switch
    {
        { Status: "Posted", JournalId: not null, Decision.IncrementalUncoveredGoldMg: var increment } =>
            increment >= 0 && increment <= quantity,
        { Status: "Rejected", JournalId: null, Decision.RejectionCode: { Length: > 0 } } => true,
        _ => false
    };
}
