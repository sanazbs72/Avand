using Microsoft.EntityFrameworkCore;

namespace Avand.Ledger.Api.Persistence;

internal static class LedgerLocks
{
    public static Task LockProducerAsync(this LedgerDbContext db, string producer, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({producer}, 0))", ct);

    /// <summary>Serializes one authenticated operation identity for replay resolution.</summary>
    public static Task LockOperationAsync(this LedgerDbContext db, string producer, Guid operationId, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({producer + "/" + operationId.ToString("D")}, 0))", ct);

    /// <summary>Takes the shared V02 gold-pool guard. Every writer that changes custody, outflow commitments or
    /// customer gold claims must hold it, and it is always taken before eligibility and account rows.</summary>
    public static async Task<GoldBacking?> LockBackingAsync(this LedgerDbContext db, CancellationToken ct)
    {
        var rows = await db.GoldBacking.FromSqlRaw("SELECT * FROM public.avand_lock_backing()").AsNoTracking().ToListAsync(ct);
        return rows.SingleOrDefault();
    }

    /// <summary>Takes the shared V03 liquidity guard. Every writer that can lower free liquidity (sale, top-up credit,
    /// cash or control changes) must hold it. Order: operation/producer lock, backing guard, liquidity guard,
    /// eligibility, then account rows by ID.</summary>
    public static async Task<LiquidityControl?> LockLiquidityAsync(this LedgerDbContext db, CancellationToken ct)
    {
        var rows = await db.LiquidityControl.FromSqlRaw("SELECT * FROM public.avand_lock_liquidity()").AsNoTracking()
            .ToListAsync(ct);
        return rows.SingleOrDefault();
    }

    /// <summary>Authoritative free liquidity in IRR, computed in one statement. Read only while holding the guard
    /// when the result authorizes a decision. Null if the control row is missing.</summary>
    public static async Task<long?> ReadFreeLiquidityAsync(this LedgerDbContext db, CancellationToken ct)
    {
        var rows = await db.Database.SqlQuery<long?>($"SELECT public.avand_free_liquidity() AS \"Value\"").ToListAsync(ct);
        return rows.Single();
    }

    public static async Task<Guid> OpenGoldAccountAsync(this LedgerDbContext db, Guid customerId, CancellationToken ct)
    {
        var ids = await db.Database.SqlQuery<Guid>($"SELECT public.avand_open_gold_account({customerId}) AS \"Value\"")
            .ToListAsync(ct);
        return ids.Single();
    }

    public static async Task<Eligibility?> LockEligibilityAsync(this LedgerDbContext db, Guid customerId, CancellationToken ct)
    {
        var rows = await db.Eligibility.FromSqlInterpolated($"""
            SELECT * FROM "Eligibility" WHERE "CustomerId" = {customerId} FOR UPDATE
            """).ToListAsync(ct);
        return rows.SingleOrDefault();
    }

    public static Task<List<Account>> LockAccountsAsync(this LedgerDbContext db, Guid[] ids, CancellationToken ct) =>
        db.Accounts.FromSqlInterpolated($"SELECT * FROM public.avand_lock_accounts({ids})").AsNoTracking().ToListAsync(ct);

    public static Task PostJournalAsync(this LedgerDbContext db, Guid journal, DateTimeOffset decidedAt, string template,
        PostingLeg[] legs, CancellationToken ct)
    {
        var ids = legs.Select(leg => leg.AccountId).ToArray();
        var debits = legs.Select(leg => leg.IsDebit).ToArray();
        var amounts = legs.Select(leg => leg.Amount).ToArray();
        return db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT public.avand_post_journal({journal}, {decidedAt}, {template}, {ids}, {debits}, {amounts})", ct);
    }
}

internal readonly record struct PostingLeg(Guid AccountId, bool IsDebit, long Amount);
