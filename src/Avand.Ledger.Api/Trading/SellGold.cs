using System.Security.Cryptography;
using System.Text.Json;
using Avand.Ledger.Api.Payments;
using Avand.Ledger.Api.Persistence;
using Avand.Trading;
using Microsoft.EntityFrameworkCore;

namespace Avand.Ledger.Api.Trading;

internal sealed record SellRequest(int SchemaVersion, Guid OperationId, string OperationType,
    BusinessReference BusinessReference, Guid CustomerId, ActorReference Actor, Guid CorrelationId, SellPayload Payload);
internal sealed record SellPayload(SellQuoteSnapshot QuoteSnapshot);
internal sealed record SellQuoteSnapshot(string Side, Guid QuoteId, DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt,
    long BidIrrPerGram, long QuantityMg, long GrossIrr, int FeeRatePpm, long FeeIrr, long NetCreditIrr,
    string PolicyVersion);
internal sealed record SellResult(Guid OperationId, string Status, Guid? JournalId, DateTimeOffset DecidedAt,
    BusinessReference BusinessReference, SellOutcome Decision);
internal sealed record SellOutcome(string? RejectionCode, Guid? OriginalOperationId);

/// <summary>F05 SellGold first decision, posting and replay for the Application.Trading producer.
/// The same protected state authorizes the decision and moves the balances: customer gold and shared liquidity.</summary>
internal sealed class SellGold(LedgerDbContext db, TimeProvider clock, TradingPolicy policy, ILogger<SellGold> logger)
{
    internal const string OperationType = "SellGold";
    internal const string BusinessKind = "GoldSellOrder";

    public async Task<IResult> ExecuteAsync(SellRequest request, CancellationToken ct)
    {
        if (!IsValid(request))
            return Results.BadRequest();

        var quote = request.Payload.QuoteSnapshot;
        var fingerprint = Fingerprint(request);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // 1. Replay under the authenticated operation identity takes precedence over every current guard.
        await db.LockOperationAsync(BuyGold.Producer, request.OperationId, ct);
        var stored = await db.SellDecisions.AsNoTracking().SingleOrDefaultAsync(x => x.OperationId == request.OperationId, ct);
        if (stored is not null)
            return stored.Fingerprint == fingerprint
                ? Results.Ok(ToResult(stored))
                : Results.Conflict(new { code = "IdentityConflict" });
        if (await db.BuyDecisions.AnyAsync(x => x.OperationId == request.OperationId, ct))
            return Results.Conflict(new { code = "IdentityConflict" });

        // 2. Fixed lock order: backing guard (sale changes customer gold claims), liquidity guard, eligibility,
        //    then the customer's gold and IRR accounts by ID.
        await db.LockBackingAsync(ct);
        await db.LockLiquidityAsync(ct);
        var eligibility = await db.LockEligibilityAsync(request.CustomerId, ct);
        var owner = request.CustomerId.ToString("D");
        var ids = await db.Accounts.Where(x => x.Owner == owner &&
                (x.Bucket == "CustomerGoldAvailable" || x.Bucket == "CustomerRialAvailable"))
            .Select(x => x.Id).ToArrayAsync(ct);
        var accounts = await db.LockAccountsAsync(ids, ct);
        var gold = accounts.SingleOrDefault(x => x.Bucket == "CustomerGoldAvailable");
        var wallet = accounts.SingleOrDefault(x => x.Bucket == "CustomerRialAvailable");
        if (wallet is null)
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

        var orderClaim = await db.SellBusinessClaims.AsNoTracking()
            .SingleOrDefaultAsync(x => x.OrderId == request.BusinessReference.Id, ct);
        var quoteClaim = await db.SellQuoteClaims.AsNoTracking().SingleOrDefaultAsync(x => x.QuoteId == quote.QuoteId, ct);
        var freeLiquidity = await db.ReadFreeLiquidityAsync(ct);

        // 3. Sample the protected decision time only after every guard is held.
        var now = clock.GetUtcNow();
        var original = orderClaim?.OperationId ?? quoteClaim?.OperationId;
        var rejection = orderClaim is not null ? "DuplicateBusinessAction"
            : quoteClaim is not null ? "QuoteAlreadyUsed"
            : Evaluate(request, eligibility, gold, wallet, freeLiquidity, now);

        Guid? journalId = null;
        if (rejection is null)
        {
            journalId = Guid.NewGuid();
            List<PostingLeg> legs =
            [
                new(gold!.Id, true, quote.QuantityMg),
                new(BuyGold.PlatformGoldPosition, false, quote.QuantityMg),
                new(BuyGold.TradingRialClearing, true, quote.GrossIrr),
                new(wallet.Id, false, quote.NetCreditIrr)
            ];
            if (quote.FeeIrr > 0)
                legs.Add(new(BuyGold.FeeRevenue, false, quote.FeeIrr));
            await db.PostJournalAsync(journalId.Value, now, "F05", [.. legs], ct);
        }

        var decision = new SellDecision
        {
            OperationId = request.OperationId,
            OrderId = request.BusinessReference.Id,
            QuoteId = quote.QuoteId,
            CustomerId = request.CustomerId,
            Fingerprint = fingerprint,
            JournalId = journalId,
            RejectionCode = rejection,
            OriginalOperationId = original,
            DecidedAt = now
        };
        db.Add(decision);
        if (original is null)
        {
            // One canonical first decision per order and per quote. Conflicting identities get their own rejection.
            db.Add(new SellBusinessClaim { OrderId = decision.OrderId, OperationId = decision.OperationId });
            db.Add(new SellQuoteClaim { QuoteId = quote.QuoteId, OperationId = decision.OperationId, CustomerId = request.CustomerId });
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        logger.LogInformation(
            "SellGold {Status} for order {OrderId}, operation {OperationId}, journal {JournalId}, reason {Reason}",
            journalId.HasValue ? "Posted" : "Rejected", decision.OrderId, decision.OperationId, journalId, rejection);
        return Results.Ok(ToResult(decision));
    }

    /// <summary>Stored sell result under the Trading producer, or null when this operation is not a sale.</summary>
    public async Task<SellResult?> FindAsync(Guid operationId, CancellationToken ct)
    {
        var decision = await db.SellDecisions.AsNoTracking().SingleOrDefaultAsync(x => x.OperationId == operationId, ct);
        return decision is null ? null : ToResult(decision);
    }

    private string? Evaluate(SellRequest request, Eligibility? eligibility, Account? gold, Account wallet,
        long? freeLiquidity, DateTimeOffset now)
    {
        var quote = request.Payload.QuoteSnapshot;
        var rate = policy.SellFeeRateFor(quote.PolicyVersion);
        if (rate is null || rate != quote.FeeRatePpm)
            return "UnsupportedQuotePolicy";
        if (!SellQuoteMath.TryForWeight(quote.QuantityMg, quote.BidIrrPerGram, quote.FeeRatePpm, out var amounts) ||
            amounts != new SellAmounts(quote.QuantityMg, quote.GrossIrr, quote.FeeIrr, quote.NetCreditIrr) ||
            quote.ExpiresAt - quote.IssuedAt != TimeSpan.FromSeconds(BuyQuoteMath.QuoteLifetimeSeconds) ||
            quote.IssuedAt > now)
            return "QuoteTermsInvalid";
        if (now >= quote.ExpiresAt)
            return "QuoteExpired";
        if (eligibility is not { IsEligible: true })
            return "CustomerNotEligible";
        if (!SellQuoteMath.IsInBounds(quote.GrossIrr, policy.MinimumIrr, policy.MaximumIrr))
            return "OrderOutOfBounds";
        if (gold is null || gold.BalanceMinor < quote.QuantityMg)
            return "InsufficientGold";
        if (freeLiquidity is not { } liquidity || liquidity < quote.NetCreditIrr)
            return "InsufficientLiquidity";
        return wallet.BalanceMinor > long.MaxValue - quote.NetCreditIrr ? "AmountOutOfRange" : null;
    }

    internal static SellResult ToResult(SellDecision decision) =>
        new(decision.OperationId, decision.JournalId.HasValue ? "Posted" : "Rejected", decision.JournalId,
            decision.DecidedAt, new BusinessReference(BusinessKind, decision.OrderId, "Execute"),
            new SellOutcome(decision.RejectionCode, decision.OriginalOperationId));

    private static string Fingerprint(SellRequest request) =>
        "v1:" + Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request with { CorrelationId = Guid.Empty })));

    internal static bool IsValid(SellRequest request) =>
        request is { SchemaVersion: 1, OperationType: OperationType } &&
        request.OperationId != Guid.Empty && request.CustomerId != Guid.Empty && request.CorrelationId != Guid.Empty &&
        request.BusinessReference is { Kind: BusinessKind, Action: "Execute" } reference && reference.Id != Guid.Empty &&
        request.Actor is { Kind: "Customer" } actor && actor.Id == request.CustomerId &&
        request.Payload?.QuoteSnapshot is { Side: "Sell" } quote && quote.QuoteId != Guid.Empty &&
        quote.BidIrrPerGram > 0 && quote.QuantityMg > 0 && quote.GrossIrr > 0 && quote.FeeIrr >= 0 &&
        quote.NetCreditIrr > 0 && quote.IssuedAt.Offset == TimeSpan.Zero && quote.ExpiresAt.Offset == TimeSpan.Zero &&
        !string.IsNullOrWhiteSpace(quote.PolicyVersion) && quote.PolicyVersion.Length <= 64;
}
