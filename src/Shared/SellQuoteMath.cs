namespace Avand.Trading;

internal readonly record struct SellAmounts(long WeightMg, long GrossIrr, long FeeIrr, long NetIrr);

/// <summary>
/// Exact V03 sell arithmetic shared by Application issuance and Ledger validation. Uses the bid side of the paired
/// price. Weight rounds down to whole mg for rial input, gross rounds down to whole IRR, fee rounds up, and net is
/// gross minus fee and must be positive. Order limits apply inclusively to gross.
/// </summary>
internal static class SellQuoteMath
{
    internal const string InitialPolicyVersion = "sell-v1";
    internal const int InitialSellFeePpm = 5_000;
    private const int MilligramsPerGram = 1000;
    private const int PartsPerMillion = 1_000_000;

    /// <summary>Quote for a gross IRR target before fee. Weight is the largest whole mg not exceeding the target.</summary>
    internal static bool TryQuoteGross(long targetGrossIrr, long bid, int rate, out SellAmounts amounts)
    {
        amounts = default;
        if (targetGrossIrr <= 0 || bid <= 0)
            return false;

        var weight = (Int128)targetGrossIrr * MilligramsPerGram / bid;
        return weight <= long.MaxValue && TryForWeight((long)weight, bid, rate, out amounts);
    }

    /// <summary>Quote for a whole-mg weight. Fails when the weight, price or rate is invalid or net is not positive.</summary>
    internal static bool TryForWeight(long weight, long bid, int rate, out SellAmounts amounts)
    {
        amounts = default;
        if (weight <= 0 || bid <= 0 || !BuyQuoteMath.IsValidRate(rate))
            return false;

        var gross = (Int128)weight * bid / MilligramsPerGram;
        if (gross > long.MaxValue)
            return false;

        var fee = (gross * rate + PartsPerMillion - 1) / PartsPerMillion;
        var net = gross - fee;
        if (net <= 0)
            return false;

        amounts = new(weight, (long)gross, (long)fee, (long)net);
        return true;
    }

    internal static bool IsInBounds(long gross, long minimum, long maximum) =>
        BuyQuoteMath.IsInBounds(gross, minimum, maximum);
}
