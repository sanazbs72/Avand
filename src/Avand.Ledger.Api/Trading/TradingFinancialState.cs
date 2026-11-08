using System.Globalization;
using Avand.Ledger.Api.Persistence;
using Avand.Trading;
using Microsoft.EntityFrameworkCore;

namespace Avand.Ledger.Api.Trading;

/// <summary>Shared V02 backing figures in mg: confirmed eligible custody C, active outgoing commitments G and all
/// customer gold claims U. Mode is null when no reliable custodian state is available.</summary>
internal readonly record struct BackingState(long ConfirmedMg, long OutgoingMg, long ClaimsMg, bool? IsOutage)
{
    /// <summary>E = max(0, C − G). An active outflow commitment is deducted exactly once.</summary>
    public long RemainingMg => Math.Max(0, ConfirmedMg - OutgoingMg);

    public long? HeadroomMg => IsOutage switch
    {
        false => Math.Max(0, RemainingMg - ClaimsMg),
        true => Math.Max(0, RemainingMg + RemainingMg / 100 - ClaimsMg),
        null => null
    };

    /// <summary>Increase in uncovered claims caused by adding <paramref name="quantityMg"/> to U.</summary>
    public long IncrementalDeficitMg(long quantityMg) =>
        Math.Max(0, ClaimsMg + quantityMg - RemainingMg) - Math.Max(0, ClaimsMg - RemainingMg);

    internal static readonly string[] CustomerGoldBuckets =
        ["CustomerGoldAvailable", "CustomerGoldReserved", "CustomerGoldRestricted"];

    /// <summary>Reads the backing inputs. Callers that authorize a purchase must already hold the backing guard.</summary>
    internal static async Task<BackingState> ReadAsync(LedgerDbContext db, GoldBacking? control, CancellationToken ct)
    {
        var custody = await db.Accounts.AsNoTracking().Where(x => x.Bucket == "CustodianGold")
            .Select(x => (long?)x.BalanceMinor).ToListAsync(ct);
        var claims = await db.Accounts.AsNoTracking().Where(x => CustomerGoldBuckets.Contains(x.Bucket))
            .SumAsync(x => (long?)x.BalanceMinor, ct) ?? 0;
        var mode = control is null || custody.Count == 0 ? null : control.IsOutage;
        return new BackingState(custody.Sum(x => x ?? 0), control?.OutgoingMg ?? 0, claims, mode);
    }
}

/// <summary>Effective Ledger trading policy. Quote policies pin the fee promised at issue; limits are current.</summary>
internal sealed class TradingPolicy(IConfiguration configuration)
{
    public int? FeeRateFor(string version)
    {
        var policies = configuration.GetSection("V02:QuotePolicies");
        if (!policies.GetChildren().Any())
            return version == BuyQuoteMath.InitialPolicyVersion ? BuyQuoteMath.InitialBuyFeePpm : null;
        return int.TryParse(policies[version], NumberStyles.None, CultureInfo.InvariantCulture, out var rate) &&
            BuyQuoteMath.IsValidRate(rate) ? rate : null;
    }

    /// <summary>Sell fee promised by a sell quote policy. Kept separate from buy policies so a version cannot be
    /// used on the wrong side.</summary>
    public int? SellFeeRateFor(string version)
    {
        var policies = configuration.GetSection("V03:SellQuotePolicies");
        if (!policies.GetChildren().Any())
            return version == SellQuoteMath.InitialPolicyVersion ? SellQuoteMath.InitialSellFeePpm : null;
        return int.TryParse(policies[version], NumberStyles.None, CultureInfo.InvariantCulture, out var rate) &&
            BuyQuoteMath.IsValidRate(rate) ? rate : null;
    }

    public long MinimumIrr => Read("V02:Limits:MinimumIrr", BuyQuoteMath.InitialMinimumIrr);
    public long MaximumIrr => Read("V02:Limits:MaximumIrr", BuyQuoteMath.InitialMaximumIrr);

    private long Read(string key, long fallback) =>
        long.TryParse(configuration[key], NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : fallback;
}

internal sealed class TradingFinancialState(LedgerDbContext db)
{
    /// <summary>Advisory, Trading-scoped committed state. Exposes headroom only, never other customers' claims.</summary>
    public async Task<IResult> ReadAsync(Guid customerId, CancellationToken ct)
    {
        var owner = customerId.ToString("D");
        var balances = await db.Accounts.AsNoTracking()
            .Where(x => x.Owner == owner && (x.Bucket == "CustomerRialAvailable" || x.Bucket == "CustomerGoldAvailable"))
            .Select(x => new { x.Bucket, x.BalanceMinor }).ToListAsync(ct);
        var wallet = balances.SingleOrDefault(x => x.Bucket == "CustomerRialAvailable");
        if (wallet is null)
            return Results.NotFound();

        var eligible = await db.Eligibility.AsNoTracking().Where(x => x.CustomerId == customerId)
            .Select(x => (bool?)x.IsEligible).SingleOrDefaultAsync(ct) == true;
        var control = await db.GoldBacking.AsNoTracking().SingleOrDefaultAsync(x => x.Id == 1, ct);
        var backing = await BackingState.ReadAsync(db, control, ct);
        return Results.Ok(new
        {
            availableIrr = wallet.BalanceMinor,
            availableGoldMg = balances.SingleOrDefault(x => x.Bucket == "CustomerGoldAvailable")?.BalanceMinor ?? 0,
            eligible,
            backing = new
            {
                status = backing.HeadroomMg is null ? "Unavailable" : "Available",
                headroomMg = backing.HeadroomMg
            }
        });
    }
}
