using Avand.Application.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Avand.Application.Api.Payments;

internal sealed record WorkLease(Guid Id, Guid TopUpId, string Kind, string Payload, long Version, int Attempts);

internal interface IRecoveryHandler
{
    Task<RecoveryCompletion> ExecuteAsync(WorkLease work, CancellationToken cancellationToken);
}

internal sealed record RecoveryCompletion(
    bool IsFinal,
    Func<ApplicationDbContext, CancellationToken, Task>? Apply = null);

internal sealed class RecoveryWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<RecoveryWorker> logger) : BackgroundService
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(10);

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
                    await Task.Delay(TimeSpan.FromMilliseconds(250), clock, stoppingToken);
                    continue;
                }

                var handler = scope.ServiceProvider.GetRequiredService<IRecoveryHandler>();
                using var timeout = new CancellationTokenSource(AttemptTimeout, clock);
                using var attempt = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, timeout.Token);
                RecoveryCompletion completion;
                try
                {
                    completion = await handler.ExecuteAsync(lease, attempt.Token);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    logger.LogWarning("Recovery work {WorkId} for {TopUpId} ({Kind}) remains unresolved: {ErrorType}",
                        lease.Id, lease.TopUpId, lease.Kind, ex.GetType().Name);
                    completion = new RecoveryCompletion(false);
                }

                await CompleteAsync(db, lease, completion, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning("Recovery iteration did not complete: {ErrorType}", ex.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(1), clock, stoppingToken);
            }
        }
    }

    private async Task<WorkLease?> ClaimAsync(ApplicationDbContext db, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var rows = await db.RecoveryWork.FromSqlInterpolated($"""
            SELECT * FROM "RecoveryWork"
            WHERE "DueAt" <= {now} AND ("LeasedUntil" IS NULL OR "LeasedUntil" <= {now})
            ORDER BY "DueAt", "Id"
            LIMIT 1 FOR UPDATE SKIP LOCKED
            """).ToListAsync(cancellationToken);
        var row = rows.SingleOrDefault();
        if (row is null)
            return null;

        row.LeasedUntil = now + LeaseDuration;
        row.Version++;
        row.Attempts = Math.Min(row.Attempts + 1, 1_000);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        db.ChangeTracker.Clear();
        return new WorkLease(row.Id, row.TopUpId, row.Kind, row.Payload, row.Version, row.Attempts);
    }

    private async Task CompleteAsync(
        ApplicationDbContext db, WorkLease lease, RecoveryCompletion completion, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var rows = await db.RecoveryWork.FromSqlInterpolated($"""
            SELECT * FROM "RecoveryWork" WHERE "Id" = {lease.Id} FOR UPDATE
            """).ToListAsync(cancellationToken);
        var row = rows.SingleOrDefault();
        if (row is null || row.Version != lease.Version || row.LeasedUntil <= clock.GetUtcNow())
            return;

        if (completion.Apply is not null)
            await completion.Apply(db, cancellationToken);

        row.LeasedUntil = null;
        // Keep the immutable command and financial intent available for audit after completion.
        row.DueAt = completion.IsFinal
            ? DateTimeOffset.MaxValue
            : clock.GetUtcNow() + TimeSpan.FromSeconds(Math.Min(60, 1 << Math.Min(lease.Attempts, 6)));

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
