using Avand.Application.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Avand.Application.Api.Payments;

internal sealed class TopUpAdmission(ApplicationDbContext db, TimeProvider clock, IConfiguration configuration,
    LedgerPaymentsClient ledger, MockGatewayAdapter gateway)
{
    public async Task<IResult> AdmitAsync(Guid customer, string key, long amount, CancellationToken ct)
    {
        if (amount <= 0 || string.IsNullOrWhiteSpace(key) || key.Length > 128)
            return Results.BadRequest();

        var original = await FindAsync(customer, key, ct);
        if (original is not null)
            return Acknowledge(original, amount);

        var id = Guid.NewGuid();
        var topUp = new TopUp
        {
            Id = id, CustomerId = customer, AdmissionKey = key, AmountIrr = amount,
            Merchant = configuration["V01:Merchant"] ?? "mock"
        };
        db.Add(topUp);
        db.Add(NewWork(id, "Initiation", clock.GetUtcNow()));
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            original = await FindAsync(customer, key, ct);
            if (original is null)
                throw;
            return Acknowledge(original, amount);
        }

        return Acknowledge(topUp, amount);
    }

    public async Task<RecoveryCompletion> ProcessAsync(TopUp topUp, CancellationToken ct)
    {
        var control = await ledger.AuthorizeAsync(topUp.Id, topUp.CustomerId, ct);
        if (control?.Status == "Rejected")
        {
            return new RecoveryCompletion(true, async (store, token) =>
            {
                var target = await store.TopUps.SingleAsync(x => x.Id == topUp.Id, token);
                target.ProviderState = "InitiationRejected";
                target.MessageCode = "FinancialEligibilityRequired";
                target.Version++;
            });
        }
        if (control?.Status != "Applied")
            return new RecoveryCompletion(false);

        var session = await gateway.CreateSessionAsync(topUp.Id, topUp.AmountIrr, ct);
        if (session is null || session.PaymentId == Guid.Empty || session.CreatedAt == default ||
            string.IsNullOrWhiteSpace(session.Continuation))
            return new RecoveryCompletion(false);

        return new RecoveryCompletion(true, async (store, token) =>
        {
            var target = await store.TopUps.SingleAsync(x => x.Id == topUp.Id, token);
            target.PaymentId = session.PaymentId.ToString("D");
            target.SessionCreatedAt = session.CreatedAt.ToUniversalTime();
            target.Continuation = session.Continuation;
            target.ProviderState = "AwaitingProvider";
            target.Version++;
            store.Add(NewWork(target.Id, "Inquiry", clock.GetUtcNow()));
        });
    }

    internal static RecoveryWork NewWork(Guid topUpId, string kind, DateTimeOffset dueAt, string payload = "") =>
        new() { Id = Guid.NewGuid(), TopUpId = topUpId, Kind = kind, Payload = payload, DueAt = dueAt };

    private Task<TopUp?> FindAsync(Guid customer, string key, CancellationToken ct) =>
        db.TopUps.AsNoTracking().SingleOrDefaultAsync(x => x.CustomerId == customer && x.AdmissionKey == key, ct);

    private static IResult Acknowledge(TopUp topUp, long amount)
    {
        if (topUp.AmountIrr != amount)
            return Results.Conflict(new { code = "IdempotencyConflict" });
        var location = $"/api/v1/wallet/top-ups/{topUp.Id:D}";
        return Results.Accepted(location, new { topUpId = topUp.Id, statusLocation = location });
    }
}
