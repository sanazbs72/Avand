using System.Globalization;
using Avand.Application.Api.Persistence;
using Avand.Trading;

namespace Avand.Application.Api.Trading;

internal sealed record QuotePolicy(string Version, int FeeRatePpm, long MinimumIrr, long MaximumIrr);

/// <summary>Currently configured issuance policy. Invalid configuration fails closed.</summary>
internal sealed class TradingPolicy(IConfiguration configuration)
{
    public QuotePolicy? Current()
    {
        var version = configuration["V02:Policy:Version"] ?? BuyQuoteMath.InitialPolicyVersion;
        var rate = Read("V02:Policy:BuyFeePpm", BuyQuoteMath.InitialBuyFeePpm);
        var minimum = Read("V02:Limits:MinimumIrr", BuyQuoteMath.InitialMinimumIrr);
        var maximum = Read("V02:Limits:MaximumIrr", BuyQuoteMath.InitialMaximumIrr);
        if (rate is not { } fee || fee >= int.MaxValue || !BuyQuoteMath.IsValidRate((int)fee) ||
            minimum is not { } min || maximum is not { } max || min <= 0 || max < min ||
            string.IsNullOrWhiteSpace(version) || version.Length > 64)
            return null;
        return new QuotePolicy(version, (int)fee, min, max);
    }

    /// <summary>Current sell issuance policy. Order limits are shared with buys (company Q2 and Q4).</summary>
    public QuotePolicy? CurrentSell()
    {
        var version = configuration["V03:Policy:SellVersion"] ?? SellQuoteMath.InitialPolicyVersion;
        var rate = Read("V03:Policy:SellFeePpm", SellQuoteMath.InitialSellFeePpm);
        var minimum = Read("V02:Limits:MinimumIrr", BuyQuoteMath.InitialMinimumIrr);
        var maximum = Read("V02:Limits:MaximumIrr", BuyQuoteMath.InitialMaximumIrr);
        if (rate is not { } fee || fee >= int.MaxValue || !BuyQuoteMath.IsValidRate((int)fee) ||
            minimum is not { } min || maximum is not { } max || min <= 0 || max < min ||
            string.IsNullOrWhiteSpace(version) || version.Length > 64)
            return null;
        return new QuotePolicy(version, (int)fee, min, max);
    }

    private long? Read(string key, long fallback)
    {
        var value = configuration[key];
        if (string.IsNullOrEmpty(value))
            return fallback;
        return long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }
}

internal sealed class BuyQuoteService(ApplicationDbContext db, IPairedPriceSource prices, TradingPolicy policy,
    LedgerTradingClient ledger, TimeProvider clock)
{
    private static readonly TimeSpan PriceMaximumAge = TimeSpan.FromSeconds(BuyQuoteMath.PriceMaximumAgeSeconds);
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(BuyQuoteMath.QuoteLifetimeSeconds);

    public async Task<IResult> IssueAsync(Guid customer, long requestedPrincipalIrr, CancellationToken ct)
    {
        if (requestedPrincipalIrr <= 0)
            return Invalid("InvalidAmount");
        if (policy.Current() is not { } terms)
            return Unavailable("QuotePolicyUnavailable");

        // Advisory only. Issuance claims no funds or capacity and an unavailable read never becomes zero.
        var state = await ledger.FinancialStateAsync(customer, ct);
        if (state is { Eligible: false })
            return Results.Json(new { code = "FinancialEligibilityRequired" }, statusCode: StatusCodes.Status403Forbidden);

        var now = clock.GetUtcNow();
        var price = prices.Current();
        var age = price is null ? TimeSpan.MinValue : now - price.ObservedAt;
        if (price is null || age < TimeSpan.Zero || age > PriceMaximumAge)
            return Unavailable("PriceUnavailable");

        if (!BuyQuoteMath.TryQuote(requestedPrincipalIrr, price.AskIrrPerGram, terms.FeeRatePpm, out var amounts))
            return Invalid("QuantityBelowOneMilligram");
        if (!BuyQuoteMath.IsInBounds(amounts.PrincipalIrr, terms.MinimumIrr, terms.MaximumIrr))
            return Invalid("OrderOutOfBounds");

        // PostgreSQL stores microseconds. Bind the issued instant at that precision so every copy agrees.
        var issuedAt = new DateTimeOffset(now.UtcTicks - now.UtcTicks % 10, TimeSpan.Zero);
        var quote = new BuyQuote
        {
            Id = Guid.NewGuid(),
            CustomerId = customer,
            RequestedPrincipalIrr = requestedPrincipalIrr,
            AskIrrPerGram = price.AskIrrPerGram,
            BidIrrPerGram = price.BidIrrPerGram,
            PriceObservedAt = price.ObservedAt,
            QuantityMg = amounts.WeightMg,
            PrincipalIrr = amounts.PrincipalIrr,
            FeeRatePpm = terms.FeeRatePpm,
            FeeIrr = amounts.FeeIrr,
            TotalDebitIrr = amounts.TotalDebitIrr,
            PolicyVersion = terms.Version,
            IssuedAt = issuedAt,
            ExpiresAt = issuedAt + Lifetime
        };
        db.Add(quote);
        await db.SaveChangesAsync(ct);

        var advisory = Advise(quote, terms, state);
        return Results.Created($"/api/v1/gold/buy-quotes/{quote.Id:D}", new
        {
            quoteId = quote.Id,
            side = "Buy",
            quote.RequestedPrincipalIrr,
            priceIrrPerGram = quote.AskIrrPerGram,
            quote.QuantityMg,
            quote.PrincipalIrr,
            quote.FeeRatePpm,
            quote.FeeIrr,
            quote.TotalDebitIrr,
            quote.IssuedAt,
            quote.ExpiresAt,
            availability = advisory.Availability,
            maximumAvailableMg = advisory.MaximumMg,
            shortfalls = advisory.Shortfalls
        });
    }

    /// <summary>Fee-inclusive advisory maximum: min(backing headroom, affordable mg, order cap mg), using the same
    /// exact arithmetic as posting. Nothing is reserved and a confirmed order is never resized to fit.</summary>
    internal static (string Availability, long? MaximumMg, string[] Shortfalls) Advise(BuyQuote quote, QuotePolicy terms,
        LedgerTradingState? state)
    {
        if (state?.Backing.HeadroomMg is not { } headroom)
            return ("Unavailable", null, []);

        var affordable = BuyQuoteMath.MaxAffordableWeight(state.AvailableIrr, quote.AskIrrPerGram, quote.FeeRatePpm);
        var cap = BuyQuoteMath.MaxWeightForPrincipal(terms.MaximumIrr, quote.AskIrrPerGram);
        var maximum = Math.Min(headroom, Math.Min(affordable, cap));
        var usable = BuyQuoteMath.TryForWeight(maximum, quote.AskIrrPerGram, quote.FeeRatePpm, out var amounts) &&
            amounts.PrincipalIrr >= terms.MinimumIrr;
        var shortfalls = new List<string>();
        if (state.AvailableIrr < quote.TotalDebitIrr)
            shortfalls.Add("InsufficientRialBalance");
        if (headroom < quote.QuantityMg)
            shortfalls.Add("InsufficientBacking");
        return usable ? ("Available", maximum, [.. shortfalls]) : ("NoQuantityAvailable", 0, [.. shortfalls]);
    }

    private static IResult Invalid(string code) => Results.BadRequest(new { code });

    private static IResult Unavailable(string code) =>
        Results.Json(new { code, retryable = true }, statusCode: StatusCodes.Status503ServiceUnavailable);
}
