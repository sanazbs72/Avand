using Avand.Ledger.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Avand.Ledger.Api.Controls;

internal sealed record ControlRequest(int SchemaVersion, Guid CommandId, string CommandType, Guid CustomerId, AuthorizationPayload Payload);
internal sealed record AuthorizationPayload(Guid PaymentIntentId);
internal sealed record ControlResult(Guid CommandId, string Status, DateTimeOffset DecidedAt);

internal sealed class TopUpAuthorization(LedgerDbContext db, TimeProvider clock)
{
    public async Task<IResult> ExecuteAsync(string producer, ControlRequest request, CancellationToken ct)
    {
        if (request.SchemaVersion != 1 || request.CommandId == Guid.Empty || request.CustomerId == Guid.Empty ||
            request.Payload is null || request.Payload.PaymentIntentId == Guid.Empty)
            return Results.BadRequest();
        if (request.CommandType != "AuthorizeTopUpInitiation")
            return Results.BadRequest(new { code = "UnsupportedOperationType" });

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.LockProducerAsync(producer, ct);
        var prior = await db.InitiationDecisions.FirstOrDefaultAsync(
            x => x.Producer == producer && (x.CommandId == request.CommandId || x.PaymentIntentId == request.Payload.PaymentIntentId), ct);
        if (prior is not null)
        {
            return prior.CommandId == request.CommandId && prior.CustomerId == request.CustomerId &&
                prior.PaymentIntentId == request.Payload.PaymentIntentId
                ? Results.Ok(ToResult(prior))
                : Results.Conflict(new { code = "IdentityConflict" });
        }

        var eligibility = await db.LockEligibilityAsync(request.CustomerId, ct);
        var decision = new InitiationDecision
        {
            Producer = producer,
            CommandId = request.CommandId,
            PaymentIntentId = request.Payload.PaymentIntentId,
            CustomerId = request.CustomerId,
            Applied = eligibility?.IsEligible == true,
            DecidedAt = clock.GetUtcNow()
        };
        db.Add(decision);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Results.Ok(ToResult(decision));
    }

    public async Task<IResult> ReadAsync(string producer, Guid commandId, CancellationToken ct)
    {
        var decision = await db.InitiationDecisions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Producer == producer && x.CommandId == commandId, ct);
        return decision is null ? Results.NotFound() : Results.Ok(ToResult(decision));
    }

    private static ControlResult ToResult(InitiationDecision decision) =>
        new(decision.CommandId, decision.Applied ? "Applied" : "Rejected", decision.DecidedAt);
}
