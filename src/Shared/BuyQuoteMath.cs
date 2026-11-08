namespace Avand.Trading;

internal readonly record struct BuyAmounts(long WeightMg, long PrincipalIrr, long FeeIrr, long TotalDebitIrr);

internal static class BuyQuoteMath
{
    internal const string InitialPolicyVersion = "buy-v1";
    internal const int QuoteLifetimeSeconds = 30;
    internal const int PriceMaximumAgeSeconds = 30;
    internal const int InitialBuyFeePpm = 10_000;
    internal const long InitialMinimumIrr = 1_000_000;
    internal const long InitialMaximumIrr = 5_000_000_000;
    private const int MilligramsPerGram = 1000;
    private const int PartsPerMillion = 1_000_000;

    internal static bool IsValidRate(int rate) => rate is >= 0 and < PartsPerMillion;

    internal static bool IsInBounds(long principal, long minimum, long maximum) =>
        minimum > 0 && maximum >= minimum && principal >= minimum && principal <= maximum;

    internal static bool TryQuote(long requested, long ask, int rate, out BuyAmounts amounts)
    {
        amounts = default;
        if (requested <= 0 || ask <= 0)
            return false;

        var weight = (Int128)requested * MilligramsPerGram / ask;
        return weight <= long.MaxValue && TryForWeight((long)weight, ask, rate, out amounts);
    }

    /// <summary>Largest whole-mg weight whose fee-inclusive debit fits <paramref name="budget"/>, or 0.</summary>
    internal static long MaxAffordableWeight(long budget, long ask, int rate)
    {
        if (budget <= 0 || ask <= 0 || !IsValidRate(rate))
            return 0;
        return MaxWeight((Int128)budget * MilligramsPerGram / ask,
            weight => TryForWeight(weight, ask, rate, out var amounts) && amounts.TotalDebitIrr <= budget);
    }

    /// <summary>Largest whole-mg weight whose final pre-fee principal does not exceed <paramref name="maximum"/>.</summary>
    internal static long MaxWeightForPrincipal(long maximum, long ask)
    {
        if (maximum <= 0 || ask <= 0)
            return 0;
        return MaxWeight(((Int128)maximum + 1) * MilligramsPerGram / ask,
            weight => (Int128)weight * ask / MilligramsPerGram <= maximum);
    }

    private static long MaxWeight(Int128 upper, Func<long, bool> fits)
    {
        long low = 0, high = (long)Int128.Min(upper, long.MaxValue);
        while (low < high)
        {
            var middle = low + (high - low + 1) / 2;
            if (fits(middle)) low = middle;
            else high = middle - 1;
        }
        return low;
    }

    internal static bool TryForWeight(long weight, long ask, int rate, out BuyAmounts amounts)
    {
        amounts = default;
        if (weight <= 0 || ask <= 0 || !IsValidRate(rate))
            return false;

        var principal = (Int128)weight * ask / MilligramsPerGram;
        if (principal > long.MaxValue)
            return false;

        var fee = (principal * rate + PartsPerMillion - 1) / PartsPerMillion;
        var total = principal + fee;
        if (total > long.MaxValue)
            return false;

        amounts = new(weight, (long)principal, (long)fee, (long)total);
        return true;
    }
}
