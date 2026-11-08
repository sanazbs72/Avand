using Avand.Trading;
using Xunit;

namespace Avand.V01.IntegrationTests;

/// <summary>Independent oracle for V03 sell arithmetic (spec AC-01, AC-01b, AC-04; clarifications C-01, C-02).
/// Expected values are written out by hand from the spec, not computed with the code under test.</summary>
public sealed class V03QuoteMathTests
{
    private const long Min = BuyQuoteMath.InitialMinimumIrr;
    private const long Max = BuyQuoteMath.InitialMaximumIrr;

    [Theory]
    [InlineData(500, 9_800_000, 5000, 4_900_000, 24_500, 4_875_500)]   // AC-01, B07
    [InlineData(1000, 1_000_001, 5000, 1_000_001, 5001, 995_000)]       // AC-01b fee 5,000.005 rounds up
    [InlineData(101, 9_900_999, 5000, 1_000_000, 5000, 995_000)]        // AC-01b gross 1,000,000.899 rounds down
    [InlineData(500, 9_800_000, 10_000, 4_900_000, 49_000, 4_851_000)]  // AC-01 configured rate
    [InlineData(500, 9_800_000, 0, 4_900_000, 0, 4_900_000)]            // zero rate gives zero fee
    public void WeightInput(long weight, long bid, int rate, long gross, long fee, long net)
    {
        Assert.True(SellQuoteMath.TryForWeight(weight, bid, rate, out var amounts));
        Assert.Equal(new SellAmounts(weight, gross, fee, net), amounts);
    }

    [Theory]
    [InlineData(4_900_001, 9_800_000, 500, 4_900_000, 24_500, 4_875_500)] // AC-01 rial input rounds weight down
    [InlineData(1_000_000, 9_999_999, 100, 999_999, 5000, 994_999)]       // AC-04 rial input at floor falls below it
    public void GrossInputRoundsWeightDown(long target, long bid, long weight, long gross, long fee, long net)
    {
        Assert.True(SellQuoteMath.TryQuoteGross(target, bid, SellQuoteMath.InitialSellFeePpm, out var amounts));
        Assert.Equal(new SellAmounts(weight, gross, fee, net), amounts);
    }

    [Theory]
    [InlineData(101, 9_900_999, true)]        // gross exactly 1,000,000; net below the floor is still allowed
    [InlineData(100, 9_999_999, false)]       // gross 999,999
    [InlineData(500_000, 10_000_000, true)]   // gross exactly 5,000,000,000
    [InlineData(500_001, 10_000_000, false)]  // gross 5,000,010,000
    public void LimitsApplyInclusivelyToGross(long weight, long bid, bool allowed)
    {
        Assert.True(SellQuoteMath.TryForWeight(weight, bid, SellQuoteMath.InitialSellFeePpm, out var amounts));
        Assert.Equal(allowed, SellQuoteMath.IsInBounds(amounts.GrossIrr, Min, Max));
    }

    [Fact]
    public void ConfiguredLimitsAreHonoured()
    {
        Assert.True(SellQuoteMath.TryForWeight(500, 9_800_000, 5000, out var amounts));
        Assert.True(SellQuoteMath.IsInBounds(amounts.GrossIrr, 4_900_000, 4_900_000));
        Assert.False(SellQuoteMath.IsInBounds(amounts.GrossIrr, 4_900_001, 10_000_000));
        Assert.False(SellQuoteMath.IsInBounds(amounts.GrossIrr, 1_000_000, 4_899_999));
    }

    [Fact]
    public void InvalidOrUnrepresentableValuesFailSafely()
    {
        Assert.False(SellQuoteMath.TryForWeight(0, 9_800_000, 5000, out _));
        Assert.False(SellQuoteMath.TryForWeight(-1, 9_800_000, 5000, out _));
        Assert.False(SellQuoteMath.TryForWeight(500, 0, 5000, out _));
        Assert.False(SellQuoteMath.TryForWeight(500, 9_800_000, -1, out _));
        Assert.False(SellQuoteMath.TryForWeight(500, 9_800_000, 1_000_000, out _));
        Assert.False(SellQuoteMath.TryForWeight(1, 999, 5000, out _));            // gross 0
        Assert.False(SellQuoteMath.TryForWeight(1, 1000, 999_999, out _));        // gross 1, fee 1, net 0
        Assert.False(SellQuoteMath.TryForWeight(long.MaxValue, long.MaxValue, 5000, out _));
        Assert.False(SellQuoteMath.TryQuoteGross(0, 9_800_000, 5000, out _));
        Assert.False(SellQuoteMath.TryQuoteGross(-5, 9_800_000, 5000, out _));
        Assert.False(SellQuoteMath.TryQuoteGross(9_799, 9_800_000, 5000, out _)); // below 1 mg
        Assert.False(SellQuoteMath.TryQuoteGross(long.MaxValue, 1, 5000, out _));
    }
}
