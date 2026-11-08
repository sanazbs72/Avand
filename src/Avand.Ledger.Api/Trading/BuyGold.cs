using System.Security.Cryptography;
using System.Text.Json;
using Avand.Ledger.Api.Payments;
using Avand.Ledger.Api.Persistence;
using Avand.Trading;
using Microsoft.EntityFrameworkCore;

namespace Avand.Ledger.Api.Trading;

internal sealed record BuyRequest(int SchemaVersion, Guid OperationId, string OperationType,
    BusinessReference BusinessReference, Guid CustomerId, ActorReference Actor, Guid CorrelationId, BuyPayload Payload);
internal sealed record BuyPayload(QuoteSnapshot QuoteSnapshot);
internal sealed record QuoteSnapshot(string Side, Guid QuoteId, DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt,
    long AskIrrPerGram, long QuantityMg, long PrincipalIrr, int FeeRatePpm, long FeeIrr, long TotalDebitIrr,
    string PolicyVersion);
internal sealed record BuyResult(Guid OperationId, string Status, Guid? JournalId, DateTimeOffset DecidedAt,
    BusinessReference BusinessReference, BuyOutcome Decision);
internal sealed record BuyOutcome(string? RejectionCode, long IncrementalUncoveredGoldMg, Guid? OriginalOperationId);

/// <summary>F04 BuyGold first decision, posting and replay for the Application.Trading producer.</summary>
internal sealed class BuyGold(LedgerDbContext db, TimeProvider clock, TradingPolicy policy, ILogger<BuyGold> logger)
{
    internal const string Producer = "Application.Trading";
    internal static readonly Guid TradingRialClearing = Guid.Parse("26020000-0000-0000-0000-000000000001");
    internal static readonly Guid FeeRevenue = Guid.Parse("26020000-0000-0000-0000-000000000002");
    internal static readonly Guid PlatformGoldPosition = Guid.Parse("26020000-0000-0000-0000-000000000003");

    public async Task<IResult> ExecuteAsync(BuyRequest request, CancellationToken ct)
    {
        if (!IsValid(request))
            return Results.BadRequest();

        var quote = request.Payload.QuoteSnapshot;
        var fingerprint = Fingerprint(request);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // 1. Replay under the authenticated operation identity takes precedence over every current guard.
        await db.LockOperationAsync(Producer, request.OperationId, ct);
        var stored = await db.BuyDecisions.AsNoTracking().SingleOrDefaultAsync(x => x.OperationId == request.OperationId, ct);
        if (stored is not null)
            return stored.Fingerprint == fingerprint
                ? Results.Ok(ToResult(stored))
                : Results.Conflict(new { code = "IdentityConflict" });

        // 2. Fixed lock order: shared backing guard, effective eligibility, then account rows by ID.
        var control = await db.LockBackingAsync(ct);
        var eligibility = await db.LockEligibilityAsync(request.CustomerId, ct);
        var owner = request.CustomerId.ToString("D");
        var walletIds = await db.Accounts.Where(x => x.Owner == owner && x.Bucket == "CustomerRialAvailable")
            .Select(x => x.Id).ToArrayAsync(ct);
        var wallet = (await db.LockAccountsAsync(walletIds, ct)).SingleOrDefault();
        var orderClaim = await db.BuyBusinessClaims.AsNoTracking()
            .SingleOrDefaultAsync(x => x.OrderId == request.BusinessReference.Id, ct);
        var quoteClaim = await db.BuyQuoteClaims.AsNoTracking().SingleOrDefaultAsync(x => x.QuoteId == quote.QuoteId, ct);
        var backing = await BackingState.ReadAsync(db, control, ct);

        // 3. Sample the protected decision time only after every guard is held.
        var now = clock.GetUtcNow();
        var original = orderClaim?.OperationId ?? quoteClaim?.OperationId;
        var rejection = orderClaim is not null ? "DuplicateBusinessAction"
            : quoteClaim is not null ? "QuoteAlreadyUsed"
            : Evaluate(request, eligibility, wallet, backing, now);

        Guid? journalId = null;
        long increment = 0;
        if (rejection is null)
        {
            increment = backing.IncrementalDeficitMg(quote.QuantityMg);
            journalId = Guid.NewGuid();
            var gold = await db.OpenGoldAccountAsync(request.CustomerId, ct);
            List<PostingLeg> legs =
            [
                new(wallet!.Id, true, quote.TotalDebitIrr),
                new(TradingRialClearing, false, quote.PrincipalIrr),
                new(PlatformGoldPosition, true, quote.QuantityMg),
                new(gold, false, quote.QuantityMg)
            ];
            if (quote.FeeIrr > 0)
                legs.Add(new(FeeRevenue, false, quote.FeeIrr));
            await db.PostJournalAsync(journalId.Value, now, "F04", [.. legs], ct);
        }

        var decision = new BuyDecision
        {
            OperationId = request.OperationId,
            OrderId = request.BusinessReference.Id,
            QuoteId = quote.QuoteId,
            CustomerId = request.CustomerId,
            Fingerprint = fingerprint,
            JournalId = journalId,
            RejectionCode = rejection,
            OriginalOperationId = original,
            IncrementalUncoveredGoldMg = increment,
            DecidedAt = now
        };
        db.Add(decision);
        if (original is null)
        {
            // One canonical first decision per order and per quote. Conflicting identities get their own rejection.
            db.Add(new BuyBusinessClaim { OrderId = decision.OrderId, OperationId = decision.OperationId });
            db.Add(new BuyQuoteClaim { QuoteId = quote.QuoteId, OperationId = decision.OperationId, CustomerId = request.CustomerId });
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        logger.LogInformation(
            "BuyGold {Status} for order {OrderId}, operation {OperationId}, journal {JournalId}, reason {Reason}, procurement increment {IncrementMg} mg",
            journalId.HasValue ? "Posted" : "Rejected", decision.OrderId, decision.OperationId, journalId, rejection, increment);
        return Results.Ok(ToResult(decision));
    }

    public async Task<IResult> ReadAsync(Guid operationId, CancellationToken ct)
    {
        var decision = await db.BuyDecisions.AsNoTracking().SingleOrDefaultAsync(x => x.OperationId == operationId, ct);
        return decision is null ? Results.NotFound() : Results.Ok(ToResult(decision));
    }

    private string? Evaluate(BuyRequest request, Eligibility? eligibility, Account? wallet, BackingState backing,
        DateTimeOffset now)
    {
        var quote = request.Payload.QuoteSnapshot;
        var rate = policy.FeeRateFor(quote.PolicyVersion);
        if (rate is null || rate != quote.FeeRatePpm)
            return "UnsupportedQuotePolicy";
        if (!BuyQuoteMath.TryForWeight(quote.QuantityMg, quote.AskIrrPerGram, quote.FeeRatePpm, out var amounts) ||
            amounts != new BuyAmounts(quote.QuantityMg, quote.PrincipalIrr, quote.FeeIrr, quote.TotalDebitIrr) ||
            quote.ExpiresAt - quote.IssuedAt != TimeSpan.FromSeconds(BuyQuoteMath.QuoteLifetimeSeconds) ||
            quote.IssuedAt > now)
            return "QuoteTermsInvalid";
        if (now >= quote.ExpiresAt)
            return "QuoteExpired";
        if (eligibility is not { IsEligible: true })
            return "CustomerNotEligible";
        if (!BuyQuoteMath.IsInBounds(quote.PrincipalIrr, policy.MinimumIrr, policy.MaximumIrr))
            return "OrderOutOfBounds";
        if (wallet is null || wallet.BalanceMinor < quote.TotalDebitIrr)
            return "InsufficientRialBalance";
        if (backing.HeadroomMg is not { } headroom)
            return "BackingEvidenceUnavailable";
        return quote.QuantityMg > headroom ? "InsufficientBacking" : null;
    }

    private static BuyResult ToResult(BuyDecision decision) =>
        new(decision.OperationId, decision.JournalId.HasValue ? "Posted" : "Rejected", decision.JournalId,
            decision.DecidedAt, new BusinessReference("GoldOrder", decision.OrderId, "Execute"),
            new BuyOutcome(decision.RejectionCode, decision.IncrementalUncoveredGoldMg, decision.OriginalOperationId));

    private static string Fingerprint(BuyRequest request) =>
        "v1:" + Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request with { CorrelationId = Guid.Empty })));

    private static bool IsValid(BuyRequest request) =>
        request is { SchemaVersion: 1, OperationType: "BuyGold" } &&
        request.OperationId != Guid.Empty && request.CustomerId != Guid.Empty && request.CorrelationId != Guid.Empty &&
        request.BusinessReference is { Kind: "GoldOrder", Action: "Execute" } reference && reference.Id != Guid.Empty &&
        request.Actor is { Kind: "Customer" } actor && actor.Id == request.CustomerId &&
        request.Payload?.QuoteSnapshot is { Side: "Buy" } quote && quote.QuoteId != Guid.Empty &&
        quote.AskIrrPerGram > 0 && quote.QuantityMg > 0 && quote.PrincipalIrr > 0 && quote.FeeIrr >= 0 &&
        quote.TotalDebitIrr > 0 && quote.IssuedAt.Offset == TimeSpan.Zero && quote.ExpiresAt.Offset == TimeSpan.Zero &&
        !string.IsNullOrWhiteSpace(quote.PolicyVersion) && quote.PolicyVersion.Length <= 64;
}
