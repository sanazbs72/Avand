using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Avand.Application.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Avand.Application.Api.Trading;

/// <summary>Atomic buy admission: the order, customer/key and quote claims, frozen Ledger request, permanent
/// operation ID and recovery work are committed together before anything is sent.</summary>
internal sealed class BuyOrderAdmission(ApplicationDbContext db, TimeProvider clock)
{
    public async Task<IResult> AdmitAsync(Guid customer, string key, Guid quoteId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 128 || quoteId == Guid.Empty)
            return Results.BadRequest(new { code = "InvalidConfirmation" });

        try
        {
            return await AdmitOnceAsync(customer, key, quoteId, ct);
        }
        catch (Exception ex) when (IsTransient(ex))
        {
            // Admission is one atomic commit, so the outcome is either nothing or the durable order. An exact
            // retry with the same key resolves which one; never report a 500 for an idempotent confirmation.
            return Results.Json(new { code = "OrderAdmissionUnavailable", retryable = true },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    /// <summary>Transient database failures: connection slots exhausted (53300), connection loss, serialization
    /// or deadlock aborts. Unique violations are not transient and are classified by the admission itself.</summary>
    private static bool IsTransient(Exception ex) =>
        ex is NpgsqlException { IsTransient: true } || ex.InnerException is NpgsqlException { IsTransient: true };

    private async Task<IResult> AdmitOnceAsync(Guid customer, string key, Guid quoteId, CancellationToken ct)
    {
        // Resolve an exact replay before classifying quote reuse, including after quote expiry.
        var original = await FindAsync(customer, key, ct);
        if (original is not null)
            return Acknowledge(original, quoteId);

        var quote = await db.BuyQuotes.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == quoteId && x.CustomerId == customer, ct);
        if (quote is null)
            return Results.NotFound();
        if (await db.BuyOrders.AnyAsync(x => x.QuoteId == quoteId, ct))
            return await ReplayOrQuoteConflictAsync(customer, key, quoteId, ct);

        var order = NewOrder(customer, key, quote);
        db.Add(order);
        db.Add(new BuyRecoveryWork { Id = Guid.NewGuid(), OrderId = order.Id, DueAt = TimeProvider.System.GetUtcNow() });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            if (await db.BuyOrders.AnyAsync(x => (x.CustomerId == customer && x.IdempotencyKey == key) || x.QuoteId == quoteId, ct))
                return await ReplayOrQuoteConflictAsync(customer, key, quoteId, ct);
            throw;
        }

        return Acknowledge(order, quoteId);
    }

    /// <summary>A concurrent identical confirmation may commit between the key lookup and the quote check.
    /// Re-resolve the customer/key claim first so an exact replay never reads as quote reuse.</summary>
    private async Task<IResult> ReplayOrQuoteConflictAsync(Guid customer, string key, Guid quoteId, CancellationToken ct)
    {
        var original = await FindAsync(customer, key, ct);
        return original is not null ? Acknowledge(original, quoteId) : QuoteConflict();
    }

    private BuyOrder NewOrder(Guid customer, string key, BuyQuote quote)
    {
        var orderId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var request = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            operationId,
            operationType = "BuyGold",
            businessReference = new { kind = "GoldOrder", id = orderId, action = "Execute" },
            customerId = customer,
            actor = new { kind = "Customer", id = customer },
            correlationId = orderId,
            payload = new
            {
                quoteSnapshot = new
                {
                    side = "Buy",
                    quoteId = quote.Id,
                    issuedAt = quote.IssuedAt,
                    expiresAt = quote.ExpiresAt,
                    askIrrPerGram = quote.AskIrrPerGram,
                    quantityMg = quote.QuantityMg,
                    principalIrr = quote.PrincipalIrr,
                    feeRatePpm = quote.FeeRatePpm,
                    feeIrr = quote.FeeIrr,
                    totalDebitIrr = quote.TotalDebitIrr,
                    policyVersion = quote.PolicyVersion
                }
            }
        });
        return new BuyOrder
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

    private Task<BuyOrder?> FindAsync(Guid customer, string key, CancellationToken ct) =>
        db.BuyOrders.AsNoTracking().SingleOrDefaultAsync(x => x.CustomerId == customer && x.IdempotencyKey == key, ct);

    private static IResult Acknowledge(BuyOrder order, Guid quoteId)
    {
        if (order.RequestHash != RequestHash(quoteId))
            return Results.Conflict(new { code = "IdempotencyConflict" });
        var location = $"/api/v1/gold/buy-orders/{order.Id:D}";
        return Results.Accepted(location, new { orderId = order.Id, quoteId = order.QuoteId, statusLocation = location });
    }

    private static IResult QuoteConflict() => Results.Conflict(new { code = "QuoteAlreadyConfirmed" });
}
