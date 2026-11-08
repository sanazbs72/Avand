using Avand.Application.Api.Persistence;
using Avand.Trading;

namespace Avand.Application.Api.Trading;

/// <summary>Issues immutable sell quotes from the bid side of a fresh paired price (V03 C-01, C-02). Issuance reserves
/// no gold or liquidity and never discloses platform liquidity. A known shortfall of the customer's own gold is
/// advisory only.</summary>
internal sealed class SellQuoteService(ApplicationDbContext db, IPairedPriceSource prices, TradingPolicy policy,
    LedgerTradingClient ledger, TimeProvider clock)
{
    private static readonly TimeSpan PriceMaximumAge = TimeSpan.FromSeconds(BuyQuoteMath.PriceMaximumAgeSeconds);
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(BuyQuoteMath.QuoteLifetimeSeconds);

    public async Task<IResult> IssueAsync(Guid customer, long? quantityMg, long? requestedGrossIrr, CancellationToken ct)
    {
        if (quantityMg.HasValue == requestedGrossIrr.HasValue)
            return Invalid("ExactlyOneAmountRequired");
        var (mode, value) = quantityMg is { } mg ? ("Weight", mg) : ("GrossIrr", requestedGrossIrr!.Value);
        if (value <= 0)
            return Invalid("InvalidAmount");
        if (policy.CurrentSell() is not { } terms)
            return Unavailable("QuotePolicyUnavailable");

        var state = await ledger.FinancialStateAsync(customer, ct);
        if (state is { Eligible: false })
            return Results.Json(new { code = "FinancialEligibilityRequired" }, statusCode: StatusCodes.Status403Forbidden);

        var now = clock.GetUtcNow();
        var price = prices.Current();
        var age = price is null ? TimeSpan.MinValue : now - price.ObservedAt;
        if (price is null || age < TimeSpan.Zero || age > PriceMaximumAge)
            return Unavailable("PriceUnavailable");

        var weight = mode == "Weight" ? value : (long)Int128.Min((Int128)value * 1000 / price.BidIrrPerGram, long.MaxValue);
        if (weight < 1)
            return Invalid("QuantityBelowOneMilligram");
        if (!SellQuoteMath.TryForWeight(weight, price.BidIrrPerGram, terms.FeeRatePpm, out var amounts))
            return Invalid("NetNotPositive");
        if (!SellQuoteMath.IsInBounds(amounts.GrossIrr, terms.MinimumIrr, terms.MaximumIrr))
            return Invalid("OrderOutOfBounds");

        // PostgreSQL stores microseconds. Bind the issued instant at that precision so every copy agrees.
        var issuedAt = new DateTimeOffset(now.UtcTicks - now.UtcTicks % 10, TimeSpan.Zero);
        var quote = new SellQuote
        {
            Id = Guid.NewGuid(),
            CustomerId = customer,
            InputMode = mode,
            InputValue = value,
            AskIrrPerGram = price.AskIrrPerGram,
            BidIrrPerGram = price.BidIrrPerGram,
            PriceObservedAt = price.ObservedAt,
            QuantityMg = amounts.WeightMg,
            GrossIrr = amounts.GrossIrr,
            FeeRatePpm = terms.FeeRatePpm,
            FeeIrr = amounts.FeeIrr,
            NetIrr = amounts.NetIrr,
            PolicyVersion = terms.Version,
            IssuedAt = issuedAt,
            ExpiresAt = issuedAt + Lifetime
        };
        db.Add(quote);
        await db.SaveChangesAsync(ct);

        string[] shortfalls = state is not null && state.AvailableGoldMg < quote.QuantityMg ? ["InsufficientGold"] : [];
        return Results.Created($"/api/v1/gold/sell-quotes/{quote.Id:D}", new
        {
            quoteId = quote.Id,
            side = "Sell",
            inputMode = quote.InputMode,
            inputValue = quote.InputValue,
            priceIrrPerGram = quote.BidIrrPerGram,
            quote.QuantityMg,
            quote.GrossIrr,
            quote.FeeRatePpm,
            quote.FeeIrr,
            quote.NetIrr,
            quote.IssuedAt,
            quote.ExpiresAt,
            availableGoldMg = state?.AvailableGoldMg,
            shortfalls
        });
    }

    private static IResult Invalid(string code) => Results.BadRequest(new { code });

    private static IResult Unavailable(string code) =>
        Results.Json(new { code, retryable = true }, statusCode: StatusCodes.Status503ServiceUnavailable);
}
