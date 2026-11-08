using Microsoft.EntityFrameworkCore;

namespace Avand.Application.Api.Persistence;

internal sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<TopUp> TopUps => Set<TopUp>();
    public DbSet<GatewayEvidence> GatewayEvidence => Set<GatewayEvidence>();
    public DbSet<RecoveryWork> RecoveryWork => Set<RecoveryWork>();
    public DbSet<BuyQuote> BuyQuotes => Set<BuyQuote>();
    public DbSet<BuyOrder> BuyOrders => Set<BuyOrder>();
    public DbSet<BuyRecoveryWork> BuyRecoveryWork => Set<BuyRecoveryWork>();
    public DbSet<ProcurementIntent> ProcurementIntents => Set<ProcurementIntent>();
    public DbSet<SellQuote> SellQuotes => Set<SellQuote>();
    public DbSet<SellOrder> SellOrders => Set<SellOrder>();
    public DbSet<SellRecoveryWork> SellRecoveryWork => Set<SellRecoveryWork>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<TopUp>(entity =>
        {
            entity.ToTable("TopUps", table => table.HasCheckConstraint("CK_TopUps_Amount", "\"AmountIrr\" > 0"));
            entity.HasIndex(x => new { x.CustomerId, x.AdmissionKey }).IsUnique();
            entity.HasIndex(x => new { x.Merchant, x.PaymentId }).IsUnique();
            entity.Property(x => x.Version).IsConcurrencyToken();
        });

        model.Entity<GatewayEvidence>(entity =>
        {
            entity.ToTable("GatewayEvidence");
            entity.HasIndex(x => new { x.Merchant, x.CaptureReference }).IsUnique();
            entity.HasIndex(x => new { x.TopUpId, x.Digest }).IsUnique();
            entity.HasOne<TopUp>().WithMany().HasForeignKey(x => x.TopUpId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<RecoveryWork>(entity =>
        {
            entity.ToTable("RecoveryWork");
            entity.HasIndex(x => new { x.TopUpId, x.Kind }).IsUnique();
            entity.HasIndex(x => x.DueAt);
            entity.Property(x => x.Version).IsConcurrencyToken();
            entity.HasOne<TopUp>().WithMany().HasForeignKey(x => x.TopUpId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<BuyQuote>(entity =>
        {
            entity.ToTable("BuyQuotes", table => table.HasCheckConstraint("CK_BuyQuotes_Terms", """
                "RequestedPrincipalIrr" > 0 AND "AskIrrPerGram" > 0 AND "BidIrrPerGram" > 0 AND "QuantityMg" > 0
                AND "PrincipalIrr" > 0 AND "FeeRatePpm" >= 0 AND "FeeIrr" >= 0
                AND "TotalDebitIrr" = "PrincipalIrr" + "FeeIrr" AND "ExpiresAt" > "IssuedAt"
                """));
            entity.HasIndex(x => x.CustomerId);
        });

        model.Entity<BuyOrder>(entity =>
        {
            entity.ToTable("BuyOrders", table => table.HasCheckConstraint("CK_BuyOrders_Status", """
                "Status" IN ('Processing', 'FinancialOutcomeUnknown', 'Completed', 'Rejected')
                """));
            entity.HasIndex(x => new { x.CustomerId, x.IdempotencyKey }).IsUnique();
            entity.HasIndex(x => x.QuoteId).IsUnique();
            entity.HasIndex(x => x.OperationId).IsUnique();
            entity.Property(x => x.Version).IsConcurrencyToken();
            entity.HasOne<BuyQuote>().WithMany().HasForeignKey(x => x.QuoteId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<BuyRecoveryWork>(entity =>
        {
            entity.ToTable("BuyRecoveryWork");
            entity.HasIndex(x => x.OrderId).IsUnique();
            entity.HasIndex(x => x.DueAt);
            entity.Property(x => x.Version).IsConcurrencyToken();
            entity.HasOne<BuyOrder>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<ProcurementIntent>(entity =>
        {
            entity.ToTable("ProcurementIntents", table =>
                table.HasCheckConstraint("CK_ProcurementIntents_Quantity", "\"UncoveredGoldMg\" > 0"));
            entity.HasIndex(x => x.OrderId).IsUnique();
            entity.HasOne<BuyOrder>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<SellQuote>(entity =>
        {
            entity.ToTable("SellQuotes", table => table.HasCheckConstraint("CK_SellQuotes_Terms", """
                "InputMode" IN ('Weight', 'GrossIrr') AND "InputValue" > 0 AND "AskIrrPerGram" > 0
                AND "BidIrrPerGram" > 0 AND "QuantityMg" > 0 AND "GrossIrr" > 0 AND "FeeRatePpm" >= 0
                AND "FeeIrr" >= 0 AND "NetIrr" > 0 AND "NetIrr" = "GrossIrr" - "FeeIrr" AND "ExpiresAt" > "IssuedAt"
                """));
            entity.HasIndex(x => x.CustomerId);
        });

        model.Entity<SellOrder>(entity =>
        {
            entity.ToTable("SellOrders", table => table.HasCheckConstraint("CK_SellOrders_Status", """
                "Status" IN ('Processing', 'FinancialOutcomeUnknown', 'Completed', 'Rejected')
                """));
            entity.HasIndex(x => new { x.CustomerId, x.IdempotencyKey }).IsUnique();
            entity.HasIndex(x => x.QuoteId).IsUnique();
            entity.HasIndex(x => x.OperationId).IsUnique();
            entity.Property(x => x.Version).IsConcurrencyToken();
            entity.HasOne<SellQuote>().WithMany().HasForeignKey(x => x.QuoteId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<SellRecoveryWork>(entity =>
        {
            entity.ToTable("SellRecoveryWork");
            entity.HasIndex(x => x.OrderId).IsUnique();
            entity.HasIndex(x => x.DueAt);
            entity.Property(x => x.Version).IsConcurrencyToken();
            entity.HasOne<SellOrder>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

/// <summary>Immutable sell quote. The input mode and value and all quoted economics are bound at issue.</summary>
internal sealed class SellQuote
{
    public Guid Id { get; init; }
    public required Guid CustomerId { get; init; }
    public required string InputMode { get; init; }
    public required long InputValue { get; init; }
    public required long AskIrrPerGram { get; init; }
    public required long BidIrrPerGram { get; init; }
    public required DateTimeOffset PriceObservedAt { get; init; }
    public required long QuantityMg { get; init; }
    public required long GrossIrr { get; init; }
    public required int FeeRatePpm { get; init; }
    public required long FeeIrr { get; init; }
    public required long NetIrr { get; init; }
    public required string PolicyVersion { get; init; }
    public required DateTimeOffset IssuedAt { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
}

/// <summary>Customer-owned sell order with its frozen Ledger request and permanent operation identity.</summary>
internal sealed class SellOrder
{
    public Guid Id { get; init; }
    public required Guid CustomerId { get; init; }
    public required Guid QuoteId { get; init; }
    public required string IdempotencyKey { get; init; }
    public required string RequestHash { get; init; }
    public required Guid OperationId { get; init; }
    public required string Request { get; init; }
    public required DateTimeOffset AdmittedAt { get; init; }
    public string Status { get; set; } = "Processing";
    public string? ReasonCode { get; set; }
    public Guid? JournalId { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public long Version { get; set; }
}

internal sealed class SellRecoveryWork
{
    public Guid Id { get; init; }
    public required Guid OrderId { get; init; }
    public required DateTimeOffset DueAt { get; set; }
    public DateTimeOffset? LeasedUntil { get; set; }
    public int Attempts { get; set; }
    public long Version { get; set; }
}

/// <summary>Immutable buy quote. All quoted economics are bound at issue.</summary>
internal sealed class BuyQuote
{
    public Guid Id { get; init; }
    public required Guid CustomerId { get; init; }
    public required long RequestedPrincipalIrr { get; init; }
    public required long AskIrrPerGram { get; init; }
    public required long BidIrrPerGram { get; init; }
    public required DateTimeOffset PriceObservedAt { get; init; }
    public required long QuantityMg { get; init; }
    public required long PrincipalIrr { get; init; }
    public required int FeeRatePpm { get; init; }
    public required long FeeIrr { get; init; }
    public required long TotalDebitIrr { get; init; }
    public required string PolicyVersion { get; init; }
    public required DateTimeOffset IssuedAt { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
}

/// <summary>Customer-owned buy order with its frozen Ledger request and permanent operation identity.</summary>
internal sealed class BuyOrder
{
    public Guid Id { get; init; }
    public required Guid CustomerId { get; init; }
    public required Guid QuoteId { get; init; }
    public required string IdempotencyKey { get; init; }
    public required string RequestHash { get; init; }
    public required Guid OperationId { get; init; }
    public required string Request { get; init; }
    public required DateTimeOffset AdmittedAt { get; init; }
    public string Status { get; set; } = "Processing";
    public string? ReasonCode { get; set; }
    public Guid? JournalId { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public long? IncrementalUncoveredGoldMg { get; set; }
    public long Version { get; set; }
}

internal sealed class BuyRecoveryWork
{
    public Guid Id { get; init; }
    public required Guid OrderId { get; init; }
    public required DateTimeOffset DueAt { get; set; }
    public DateTimeOffset? LeasedUntil { get; set; }
    public int Attempts { get; set; }
    public long Version { get; set; }
}

/// <summary>Positive incremental backing deficit attributed to one posted purchase. V06 executes these separately.</summary>
internal sealed class ProcurementIntent
{
    public Guid Id { get; init; }
    public required Guid OrderId { get; init; }
    public required Guid OperationId { get; init; }
    public required Guid JournalId { get; init; }
    public required long UncoveredGoldMg { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

internal sealed class TopUp
{
    public Guid Id { get; init; }
    public required Guid CustomerId { get; init; }
    public required string AdmissionKey { get; init; }
    public required long AmountIrr { get; init; }
    public required string Merchant { get; init; }
    public string? PaymentId { get; set; }
    public DateTimeOffset? SessionCreatedAt { get; set; }
    public string? Continuation { get; set; }
    public string ProviderState { get; set; } = "Initiating";
    public string? PostingState { get; set; }
    public string? MessageCode { get; set; }
    public string? CreditedBucket { get; set; }
    public Guid? JournalId { get; set; }
    public long Version { get; set; }
}

internal sealed class GatewayEvidence
{
    public Guid Id { get; init; }
    public required Guid TopUpId { get; init; }
    public required string Merchant { get; init; }
    public required string PaymentId { get; init; }
    public string? CaptureReference { get; init; }
    public long? AmountIrr { get; init; }
    public required string Outcome { get; init; }
    public required string Digest { get; init; }
    public required DateTimeOffset ObservedAt { get; init; }
}

internal sealed class RecoveryWork
{
    public Guid Id { get; init; }
    public required Guid TopUpId { get; init; }
    public required string Kind { get; init; }
    public required string Payload { get; init; }
    public required DateTimeOffset DueAt { get; set; }
    public DateTimeOffset? LeasedUntil { get; set; }
    public int Attempts { get; set; }
    public long Version { get; set; }
}
