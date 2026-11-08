using Avand.Application.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Avand.Application.Api.Trading;

internal sealed record BuyWorkLease(Guid Id, Guid OrderId, long Version, int Attempts);

/// <summary>Leased, order-owned buy work. Reuses the V01 lease/version pattern: dispatch resumes after restart,
/// inquiry and retry use the original identity and frozen request, and transient failures back off within a bound.
/// Leases and due times are operational and use the system clock, independent of business-time controls.</summary>
internal sealed class BuyRecoveryWorker(IServiceScopeFactory scopeFactory, ILogger<BuyRecoveryWorker> logger) : BackgroundService
{
    private static readonly TimeProvider Operational = TimeProvider.System;
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan MaximumBackoff = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var lease = await ClaimAsync(db, stoppingToken);
                if (lease is null)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(200), stoppingToken);
                    continue;
                }

                var delivery = scope.ServiceProvider.GetRequiredService<BuyOrderDelivery>();
                using var attempt = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                attempt.CancelAfter(AttemptTimeout);
                BuyCompletion completion;
                try
                {
                    completion = await delivery.ProcessAsync(lease.OrderId, attempt.Token);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    logger.LogWarning("Buy work {WorkId} for order {OrderId} remains unresolved: {ErrorType}",
                        lease.Id, lease.OrderId, ex.GetType().Name);
                    completion = new BuyCompletion(false);
                }

                await CompleteAsync(db, lease, completion, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning("Buy recovery iteration did not complete: {ErrorType}", ex.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }
    }

    private static async Task<BuyWorkLease?> ClaimAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var now = Operational.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var rows = await db.BuyRecoveryWork.FromSqlInterpolated($"""
            SELECT * FROM "BuyRecoveryWork"
            WHERE "DueAt" <= {now} AND ("LeasedUntil" IS NULL OR "LeasedUntil" <= {now})
            ORDER BY "DueAt", "Id"
            LIMIT 1 FOR UPDATE SKIP LOCKED
            """).ToListAsync(ct);
        var row = rows.SingleOrDefault();
        if (row is null)
            return null;

        row.LeasedUntil = now + LeaseDuration;
        row.Version++;
        row.Attempts = Math.Min(row.Attempts + 1, 1_000);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return new BuyWorkLease(row.Id, row.OrderId, row.Version, row.Attempts);
    }

    private async Task CompleteAsync(ApplicationDbContext db, BuyWorkLease lease, BuyCompletion completion,
        CancellationToken ct)
    {
        try
        {
            await FinishAsync(db, lease, completion, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Local result application failed after the Ledger decided. Release the lease and retry the same identity.
            logger.LogWarning("Buy work {WorkId} for order {OrderId}: result application failed ({ErrorType}); rescheduled",
                lease.Id, lease.OrderId, ex.GetType().Name);
            db.ChangeTracker.Clear();
            await FinishAsync(db, lease, new BuyCompletion(false), ct);
        }
    }

    private static async Task FinishAsync(ApplicationDbContext db, BuyWorkLease lease, BuyCompletion completion,
        CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var rows = await db.BuyRecoveryWork.FromSqlInterpolated($"""
            SELECT * FROM "BuyRecoveryWork" WHERE "Id" = {lease.Id} FOR UPDATE
            """).ToListAsync(ct);
        var row = rows.SingleOrDefault();
        var now = Operational.GetUtcNow();
        if (row is null || row.Version != lease.Version || row.LeasedUntil <= now)
            return;

        if (completion.Apply is not null)
            await completion.Apply(db, ct);

        row.LeasedUntil = null;
        // The completed work stays for audit; the frozen request lives on the order.
        row.DueAt = completion.IsFinal ? DateTimeOffset.MaxValue : now + Backoff(lease.Attempts);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private static TimeSpan Backoff(int attempts) =>
        TimeSpan.FromMilliseconds(Math.Min(MaximumBackoff.TotalMilliseconds, 250 * Math.Pow(2, Math.Min(attempts - 1, 10))));
}
