using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Avand.Ledger.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Avand.Ledger.Api.Payments;

internal sealed record FinancialRequest(int SchemaVersion, Guid OperationId, string OperationType,
    BusinessReference BusinessReference, Guid CustomerId, ActorReference Actor, Guid CorrelationId, CapturePayload Payload);
internal sealed record BusinessReference(string Kind, Guid Id, string Action);
internal sealed record ActorReference(string Kind, Guid Id);
internal sealed record CapturePayload(Guid PaymentId, string CaptureRef, string CapturedIrr, Guid EvidenceBatchId,
    JsonElement[] ConfirmedReversals, EvidenceReference EvidenceRef);
internal sealed record EvidenceReference(Guid Id, string Digest);
internal sealed record FinancialResult(Guid OperationId, string Status, Guid? JournalId, DateTimeOffset DecidedAt,
    FinancialDetail Decision);
internal sealed record FinancialDetail(string? CreditedBucket, string? RejectionCode);

internal sealed class GatewayCapturePosting(LedgerDbContext db, TimeProvider clock, IConfiguration configuration)
{
    public async Task<IResult> ExecuteAsync(string producer, FinancialRequest request, CancellationToken ct)
    {
        if (request.OperationType != "ApplyGatewayPaymentEvidence")
            return Results.BadRequest(new { code = "UnsupportedOperationType" });
        if (!IsValid(request) || !long.TryParse(request.Payload.CapturedIrr, NumberStyles.None,
            CultureInfo.InvariantCulture, out var amount) || amount <= 0 ||
            request.Payload.CapturedIrr != amount.ToString(CultureInfo.InvariantCulture))
            return Results.BadRequest();

        var payload = request.Payload;
        var merchant = configuration["V01:Merchant"] ?? "mock";
        var businessKey = $"WalletTopUp/{request.BusinessReference.Id:D}/Capture";
        var canonical = request with { CorrelationId = Guid.Empty };
        var fingerprint = "v1:" + Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(canonical)));
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.LockProducerAsync(producer, ct);
        var original = await db.OperationDecisions.SingleOrDefaultAsync(
            x => x.Producer == producer && x.OperationId == request.OperationId, ct);
        if (original is not null)
            return original.Fingerprint == fingerprint ? Results.Ok(ToResult(original)) : Conflict();

        if (await db.OperationDecisions.AnyAsync(x =>
            (x.Producer == producer && x.BusinessReference == businessKey) ||
            (x.Merchant == merchant && x.CaptureReference == payload.CaptureRef), ct))
            return Conflict();

        var authorization = await db.InitiationDecisions.AsNoTracking().SingleOrDefaultAsync(
            x => x.Producer == producer && x.PaymentIntentId == request.BusinessReference.Id, ct);
        // A credit against a gateway receivable raises customer cash claims without adding bank cash, so it lowers
        // free liquidity and must serialize with sales (V03 R3). It never rejects for liquidity.
        await db.LockLiquidityAsync(ct);
        var eligibility = await db.LockEligibilityAsync(request.CustomerId, ct);
        var now = clock.GetUtcNow();
        string? rejection = null;
        Guid? journalId = null;
        string? bucket = null;
        if (authorization is not { Applied: true } || authorization.CustomerId != request.CustomerId || eligibility is null)
            rejection = "InitiationNotAuthorized";
        else
        {
            bucket = eligibility.IsEligible ? "CustomerRialAvailable" : "CustomerRialRestricted";
            var customer = request.CustomerId.ToString("D");
            var ids = await db.Accounts.Where(a => (a.Owner == customer && a.Bucket == bucket) ||
                (a.Owner == merchant && a.Bucket == "GatewayReceivable")).Select(a => a.Id).ToArrayAsync(ct);
            var accounts = await db.LockAccountsAsync(ids, ct);
            var wallet = accounts.SingleOrDefault(x => x.Bucket == bucket);
            var receivable = accounts.SingleOrDefault(x => x.Bucket == "GatewayReceivable");
            if (wallet is null || receivable is null)
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

            if (wallet.BalanceMinor > long.MaxValue - amount || receivable.BalanceMinor > long.MaxValue - amount)
                rejection = "AmountOutOfRange";
            else
            {
                journalId = Guid.NewGuid();
                await db.PostJournalAsync(journalId.Value, now, "F02",
                    [new(receivable.Id, true, amount), new(wallet.Id, false, amount)], ct);
            }
        }

        var decision = new OperationDecision
        {
            Producer = producer,
            OperationId = request.OperationId,
            BusinessReference = businessKey,
            Merchant = journalId.HasValue ? merchant : null,
            CaptureReference = journalId.HasValue ? payload.CaptureRef : null,
            Fingerprint = fingerprint,
            CustomerId = request.CustomerId,
            AmountIrr = amount,
            JournalId = journalId,
            CreditedBucket = journalId.HasValue ? bucket : null,
            RejectionCode = rejection,
            DecidedAt = now
        };
        db.Add(decision);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Results.Ok(ToResult(decision));
    }

    public async Task<IResult> ReadAsync(string producer, Guid operationId, CancellationToken ct)
    {
        var decision = await db.OperationDecisions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Producer == producer && x.OperationId == operationId, ct);
        return decision is null ? Results.NotFound() : Results.Ok(ToResult(decision));
    }

    public async Task<IResult> BalanceAsync(Guid customerId, CancellationToken ct)
    {
        var owner = customerId.ToString("D");
        var available = await db.Accounts.AsNoTracking().Where(x => x.Owner == owner && x.Bucket == "CustomerRialAvailable")
            .Select(x => (long?)x.BalanceMinor).SingleOrDefaultAsync(ct);
        return available is null ? Results.NotFound() : Results.Ok(new { availableIrr = available.Value });
    }

    private static IResult Conflict() => Results.Conflict(new { code = "IdentityConflict" });

    private static FinancialResult ToResult(OperationDecision decision) =>
        new(decision.OperationId, decision.JournalId.HasValue ? "Posted" : "Rejected", decision.JournalId,
            decision.DecidedAt, new FinancialDetail(decision.CreditedBucket, decision.RejectionCode));

    private static bool IsValid(FinancialRequest request) =>
        request.SchemaVersion == 1 && request.OperationId != Guid.Empty && request.CustomerId != Guid.Empty &&
        request.CorrelationId != Guid.Empty &&
        request.BusinessReference is { Kind: "WalletTopUp", Action: "Capture" } reference && reference.Id != Guid.Empty &&
        request.Actor is { Kind: "Customer" } actor && actor.Id == request.CustomerId &&
        request.Payload is { ConfirmedReversals.Length: 0 } payload &&
        payload.PaymentId != Guid.Empty && payload.EvidenceBatchId != Guid.Empty &&
        !string.IsNullOrWhiteSpace(payload.CaptureRef) && payload.CaptureRef.Length <= 200 &&
        payload.EvidenceRef is { Digest.Length: 64 } evidence && evidence.Id != Guid.Empty &&
        evidence.Digest.All(Uri.IsHexDigit);
}
