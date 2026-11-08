using Avand.Trading;
using Xunit;

namespace Avand.V01.IntegrationTests;

public sealed class V02QuoteMathTests
{
    [Theory]
    [InlineData(10_000_001, 10_000_000, 10_000, 1000, 10_000_000, 100_000, 10_100_000)]
    [InlineData(1_000_002, 10_000_019, 10_000, 100, 1_000_001, 10_001, 1_010_002)]
    [InlineData(1_000_101, 10_001_010, 5000, 100, 1_000_101, 5001, 1_005_102)]
    [InlineData(1_000_002, 10_000_019, 0, 100, 1_000_001, 0, 1_000_001)]
    public void ExactQuote(long requested, long ask, int rate, long weight, long principal, long fee, long total)
    {
        Assert.True(BuyQuoteMath.TryQuote(requested, ask, rate, out var quote));
        Assert.Equal(new BuyAmounts(weight, principal, fee, total), quote);
    }

    [Theory]
    [InlineData(999_999, false)]
    [InlineData(1_000_000, true)]
    [InlineData(5_000_000_000, true)]
    [InlineData(5_000_000_001, false)]
    public void LimitsAreInclusiveOnPrincipal(long principal, bool allowed) =>
        Assert.Equal(allowed, BuyQuoteMath.IsInBounds(principal,
            BuyQuoteMath.InitialMinimumIrr, BuyQuoteMath.InitialMaximumIrr));

    [Fact]
    public void InvalidOrUnrepresentableValuesFailSafely()
    {
        Assert.False(BuyQuoteMath.TryQuote(1, 0, 10_000, out _));
        Assert.False(BuyQuoteMath.TryQuote(1, 100_000, 10_000, out _));
        Assert.False(BuyQuoteMath.TryQuote(long.MaxValue, 1, 10_000, out _));
        Assert.False(BuyQuoteMath.TryForWeight(long.MaxValue, long.MaxValue, 10_000, out _));
        Assert.False(BuyQuoteMath.TryForWeight(1000, long.MaxValue, 10_000, out _));
        Assert.False(BuyQuoteMath.TryQuote(1_000_000, 10_000_000, -1, out _));
    }
}
