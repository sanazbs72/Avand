using System.Text.Json;
using Avand.Application.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Avand.Application.Api.Payments;

internal sealed class LedgerDelivery(LedgerPaymentsClient ledger, ILogger<LedgerDelivery> logger)
{
    public async Task<RecoveryCompletion> ProcessAsync(WorkLease work, CancellationToken ct)
    {
        var capture = JsonSerializer.Deserialize<LedgerCapture>(work.Payload)
            ?? throw new InvalidOperationException("Missing saved Ledger intent.");
        var decision = await ledger.DeliverAsync(capture, ct);
        var posted = decision is { Status: "Posted", JournalId: not null } &&
            decision.Decision?.CreditedBucket is "CustomerRialAvailable" or "CustomerRialRestricted";
        if (!posted && decision?.Status != "Rejected")
            return new RecoveryCompletion(false);

        return new RecoveryCompletion(true, async (db, token) =>
        {
            var target = await db.TopUps.SingleAsync(x => x.Id == work.TopUpId, token);
            target.PostingState = posted ? "Succeeded" : "PostingException";
            target.JournalId = decision!.JournalId;
            target.CreditedBucket = decision.Decision?.CreditedBucket;
            target.MessageCode = !posted ? "PostingRequiresReview" :
                target.CreditedBucket == "CustomerRialRestricted" ? "CreditRestricted" : "WalletCredited";
            target.Version++;
            logger.LogInformation("Ledger result {Result} for top-up {TopUpId}, operation {OperationId}, journal {JournalId}",
                decision.Status, target.Id, capture.OperationId, decision.JournalId);
        });
    }
}

internal sealed class PaymentRecoveryHandler(ApplicationDbContext db, TopUpAdmission admission,
    GatewayEvidenceWorkflow evidence, LedgerDelivery delivery) : IRecoveryHandler
{
    public async Task<RecoveryCompletion> ExecuteAsync(WorkLease work, CancellationToken cancellationToken)
    {
        if (work.Kind == "Ledger")
            return await delivery.ProcessAsync(work, cancellationToken);
        var topUp = await db.TopUps.AsNoTracking().SingleAsync(x => x.Id == work.TopUpId, cancellationToken);
        return work.Kind switch
        {
            "Initiation" => await admission.ProcessAsync(topUp, cancellationToken),
            "Inquiry" => await evidence.ProcessAsync(topUp, cancellationToken),
            _ => throw new InvalidOperationException("Unknown recovery work type.")
        };
    }
}
