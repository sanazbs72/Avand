using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Avand.Application.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Avand.Application.Api.Trading;

/// <summary>Atomic sell admission: the order, customer/key and quote claims, frozen SellGold request, permanent
/// operation ID and recovery work are committed together before anything is sent.</summary>
internal sealed class SellOrderAdmission(ApplicationDbContext db, TimeProvider clock)
{
    public async Task<IResult> AdmitAsync(Guid customer, string key, Guid quoteId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 128 || quoteId == Guid.Empty)
            return Results.BadRequest(new { code = "InvalidConfirmation" });

        try
        {
            return await AdmitOnceAsync(customer, key, quoteId, ct);
        }
        catch (Exception ex) when (ex is NpgsqlException { IsTransient: true } ||
                                   ex.InnerException is NpgsqlException { IsTransient: true })
        {
            // One atomic commit: nothing or the durable order. An exact retry with the same key resolves which.
            return Results.Json(new { code = "OrderAdmissionUnavailable", retryable = true },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private async Task<IResult> AdmitOnceAsync(Guid customer, string key, Guid quoteId, CancellationToken ct)
    {
        // Resolve an exact replay before classifying quote reuse, including after quote expiry.
        var original = await FindAsync(customer, key, ct);
        if (original is not null)
            return Acknowledge(original, quoteId);

        var quote = await db.SellQuotes.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == quoteId && x.CustomerId == customer, ct);
        if (quote is null)
            return Results.NotFound();
        if (await db.SellOrders.AnyAsync(x => x.QuoteId == quoteId, ct))
            return await ReplayOrQuoteConflictAsync(customer, key, quoteId, ct);

        var order = NewOrder(customer, key, quote);
        db.Add(order);
        db.Add(new SellRecoveryWork { Id = Guid.NewGuid(), OrderId = order.Id, DueAt = TimeProvider.System.GetUtcNow() });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            if (await db.SellOrders.AnyAsync(x => (x.CustomerId == customer && x.IdempotencyKey == key) || x.QuoteId == quoteId, ct))
                return await ReplayOrQuoteConflictAsync(customer, key, quoteId, ct);
            throw;
        }

        return Acknowledge(order, quoteId);
    }

    private async Task<IResult> ReplayOrQuoteConflictAsync(Guid customer, string key, Guid quoteId, CancellationToken ct)
    {
        var original = await FindAsync(customer, key, ct);
        return original is not null ? Acknowledge(original, quoteId) : Results.Conflict(new { code = "QuoteAlreadyConfirmed" });
    }

    private SellOrder NewOrder(Guid customer, string key, SellQuote quote)
    {
        var orderId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var request = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            operationId,
            operationType = "SellGold",
            businessReference = new { kind = "GoldSellOrder", id = orderId, action = "Execute" },
            customerId = customer,
            actor = new { kind = "Customer", id = customer },
            correlationId = orderId,
            payload = new
            {
                quoteSnapshot = new
                {
                    side = "Sell",
                    quoteId = quote.Id,
                    issuedAt = quote.IssuedAt,
                    expiresAt = quote.ExpiresAt,
                    bidIrrPerGram = quote.BidIrrPerGram,
                    quantityMg = quote.QuantityMg,
                    grossIrr = quote.GrossIrr,
                    feeRatePpm = quote.FeeRatePpm,
                    feeIrr = quote.FeeIrr,
                    netCreditIrr = quote.NetIrr,
                    policyVersion = quote.PolicyVersion
                }
            }
        });
        return new SellOrder
        {
            Id = orderId,
            CustomerId = customer,
            QuoteId = quote.Id,
            IdempotencyKey = key,
            RequestHash = RequestHash(quote.Id),
            OperationId = operationId,
            Request = request,
            AdmittedAt = clock.GetUtcNow()
        };
    }

    /// <summary>Canonical customer request: the confirmation carries only the quote identity (version 1).</summary>
    private static string RequestHash(Guid quoteId) =>
        "v1:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(quoteId.ToString("D"))));

    private Task<SellOrder?> FindAsync(Guid customer, string key, CancellationToken ct) =>
        db.SellOrders.AsNoTracking().SingleOrDefaultAsync(x => x.CustomerId == customer && x.IdempotencyKey == key, ct);

    private static IResult Acknowledge(SellOrder order, Guid quoteId)
    {
        if (order.RequestHash != RequestHash(quoteId))
            return Results.Conflict(new { code = "IdempotencyConflict" });
        var location = $"/api/v1/gold/sell-orders/{order.Id:D}";
        return Results.Accepted(location, new { orderId = order.Id, quoteId = order.QuoteId, statusLocation = location });
    }
}
