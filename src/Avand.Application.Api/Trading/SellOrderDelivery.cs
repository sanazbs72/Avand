using Avand.Application.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Avand.Application.Api.Trading;

/// <summary>Sends the stored frozen SellGold request under the original operation ID and applies a definitive result.
/// A sale creates no procurement intent.</summary>
internal sealed class SellOrderDelivery(ApplicationDbContext db, LedgerTradingClient ledger, ILogger<SellOrderDelivery> logger)
{
    internal static bool IsTerminal(string status) => status is "Completed" or "Rejected";

    public async Task<BuyCompletion> ProcessAsync(Guid orderId, CancellationToken ct)
    {
        var order = await db.SellOrders.AsNoTracking().SingleAsync(x => x.Id == orderId, ct);
        if (IsTerminal(order.Status))
            return new BuyCompletion(true);

        LedgerSellResult? result;
        try
        {
            result = await ledger.DeliverSellAsync(order.OperationId, order.Request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            // Transport ambiguity: the Ledger may have committed. Keep the order unresolved and recover by identity.
            logger.LogWarning("Sell order {OrderId}, operation {OperationId}: Ledger outcome unknown ({ErrorType})",
                order.Id, order.OperationId, ex.GetType().Name);
            result = null;
        }

        if (result is null || !IsDefinitive(result))
            return new BuyCompletion(false, (store, token) => MarkUnknownAsync(store, order.Id, token));
        return new BuyCompletion(true, (store, token) => ApplyAsync(store, order.Id, result, token));
    }

    /// <summary>Applies the original definitive outcome once. A repeated application leaves the order unchanged.</summary>
    internal async Task ApplyAsync(ApplicationDbContext store, Guid orderId, LedgerSellResult result, CancellationToken ct)
    {
        var rows = await store.SellOrders.FromSqlInterpolated($"""
            SELECT * FROM "SellOrders" WHERE "Id" = {orderId} FOR UPDATE
            """).ToListAsync(ct);
        var order = rows.Single();
        if (IsTerminal(order.Status))
            return;

        var posted = result.Status == "Posted";
        order.Status = posted ? "Completed" : "Rejected";
        order.ReasonCode = posted ? null : result.Decision?.RejectionCode;
        order.JournalId = result.JournalId;
        order.DecidedAt = result.DecidedAt;
        order.Version++;
        logger.LogInformation("Sell order {OrderId}, operation {OperationId}: applied {Status}, journal {JournalId}, reason {Reason}",
            orderId, order.OperationId, result.Status, result.JournalId, order.ReasonCode);
    }

    private static async Task MarkUnknownAsync(ApplicationDbContext store, Guid orderId, CancellationToken ct)
    {
        var order = await store.SellOrders.SingleAsync(x => x.Id == orderId, ct);
        if (order.Status != "Processing")
            return;
        order.Status = "FinancialOutcomeUnknown";
        order.Version++;
    }

    private static bool IsDefinitive(LedgerSellResult result) => result switch
    {
        { Status: "Posted", JournalId: not null } => true,
        { Status: "Rejected", JournalId: null, Decision.RejectionCode: { Length: > 0 } } => true,
        _ => false
    };
}
