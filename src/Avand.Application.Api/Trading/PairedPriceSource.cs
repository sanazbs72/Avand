using System.Globalization;

namespace Avand.Application.Api.Trading;

/// <summary>A paired buy/sell observation with the provider's own UTC observation time.</summary>
internal sealed record PricePair(long AskIrrPerGram, long BidIrrPerGram, DateTimeOffset ObservedAt);

internal interface IPairedPriceSource
{
    /// <summary>The latest valid pair, or null when the price or its provider timestamp is absent or invalid.</summary>
    PricePair? Current();
}

/// <summary>Development adapter that reads a controlled synthetic observation from configuration.</summary>
internal sealed class SyntheticPairedPriceSource(IConfiguration configuration) : IPairedPriceSource
{
    public PricePair? Current()
    {
        var section = configuration.GetSection("V02:SyntheticPrice");
        if (!long.TryParse(section["Ask"], NumberStyles.None, CultureInfo.InvariantCulture, out var ask) ||
            !long.TryParse(section["Bid"], NumberStyles.None, CultureInfo.InvariantCulture, out var bid) ||
            !DateTimeOffset.TryParseExact(section["ObservedAt"], "O", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var observedAt) ||
            observedAt.Offset != TimeSpan.Zero || ask <= 0 || bid <= 0 || bid > ask)
            return null;
        return new PricePair(ask, bid, observedAt);
    }
}
