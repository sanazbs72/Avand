using System.Security.Cryptography;
using System.Text.Json;
using Avand.Application.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Avand.Application.Api.Payments;

internal sealed class GatewayEvidenceWorkflow(ApplicationDbContext db, MockGatewayAdapter gateway,
    TimeProvider clock, IConfiguration configuration, ILogger<GatewayEvidenceWorkflow> logger)
{
    public async Task<IResult> NotifyAsync(Guid paymentId, CancellationToken ct)
    {
        if (paymentId == Guid.Empty)
            return Results.BadRequest();
        var merchant = configuration["V01:Merchant"] ?? "mock";
        var reference = paymentId.ToString("D");
        var now = clock.GetUtcNow();
        await db.RecoveryWork.Where(x => x.Kind == "Inquiry" && x.LeasedUntil == null && x.DueAt > now &&
            x.DueAt != DateTimeOffset.MaxValue &&
            db.TopUps.Any(t => t.Id == x.TopUpId && t.Merchant == merchant && t.PaymentId == reference))
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.DueAt, now), ct);
        return Results.Accepted();
    }

    public async Task<RecoveryCompletion> ProcessAsync(TopUp topUp, CancellationToken ct)
    {
        if (!Guid.TryParse(topUp.PaymentId, out var payment))
            return new RecoveryCompletion(false);

        var alreadyFailed = topUp.ProviderState == "Failed";
        var result = await gateway.ResolveAsync(payment, !alreadyFailed, ct);
        if (result is null || result.PaymentId != payment)
            return new RecoveryCompletion(false);

        if (result.Outcome == "Captured" && result.AmountIrr == topUp.AmountIrr &&
            !string.IsNullOrWhiteSpace(result.CaptureReference) && result.CaptureReference.Length <= 200)
        {
            if (alreadyFailed)
            {
                logger.LogWarning("Capture contradicts a definitive rejection for top-up {TopUpId}", topUp.Id);
                return new RecoveryCompletion(false);
            }

            var reference = result.CaptureReference;
            var digest = Digest(result);
            return new RecoveryCompletion(true, async (store, token) =>
            {
                await store.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_xact_lock(hashtextextended({topUp.Merchant + "/" + reference}, 0))", token);
                var target = await store.TopUps.SingleAsync(x => x.Id == topUp.Id, token);
                var exists = await store.GatewayEvidence.AnyAsync(
                    x => x.Merchant == target.Merchant && x.CaptureReference == reference, token);
                if (exists)
                {
                    logger.LogWarning("Duplicate capture ownership conflict for top-up {TopUpId}", target.Id);
                    // Keep the original work pending for inquiry and review.
                    throw new InvalidOperationException("Capture ownership conflict.");
                }

                var evidence = AddEvidence(store, target, result, digest, reference);
                target.ProviderState = "Captured";
                target.PostingState = "VerifiedPendingLedger";
                target.MessageCode = "AwaitingLedger";
                target.Version++;
                var operation = new LedgerCapture(Guid.NewGuid(), target.Id, target.CustomerId, payment,
                    reference, target.AmountIrr, evidence, digest);
                store.Add(TopUpAdmission.NewWork(target.Id, "Ledger", clock.GetUtcNow(), JsonSerializer.Serialize(operation)));
            });
        }

        if (result.Outcome is "Expired" or "Rejected" or "IssuerReturned")
        {
            return new RecoveryCompletion(result.Outcome == "IssuerReturned", async (store, token) =>
            {
                var target = await store.TopUps.SingleAsync(x => x.Id == topUp.Id, token);
                var digest = Digest(result);
                if (!await store.GatewayEvidence.AnyAsync(x => x.TopUpId == target.Id && x.Digest == digest, token))
                    AddEvidence(store, target, result, digest, null);
                target.ProviderState = "Failed";
                target.MessageCode = result.Outcome switch
                {
                    "IssuerReturned" => "IssuerReturnConfirmed",
                    "Expired" => "VerifyExpiredIssuerReturnWithin72Hours",
                    _ => "ProviderRejected"
                };
                target.Version++;
            });
        }

        return new RecoveryCompletion(false);
    }

    private Guid AddEvidence(ApplicationDbContext store, TopUp target, GatewayResult result, string digest, string? captureReference)
    {
        var id = Guid.NewGuid();
        store.Add(new GatewayEvidence
        {
            Id = id, TopUpId = target.Id, Merchant = target.Merchant, PaymentId = result.PaymentId.ToString("D"),
            CaptureReference = captureReference, AmountIrr = result.AmountIrr,
            Outcome = result.Outcome, Digest = digest, ObservedAt = clock.GetUtcNow()
        });
        return id;
    }

    private static string Digest(GatewayResult result) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(result)));
}
