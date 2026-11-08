using Microsoft.EntityFrameworkCore;

namespace Avand.Ledger.Api.Persistence;

internal sealed class LedgerDbContext(DbContextOptions<LedgerDbContext> options) : DbContext(options)
{
    public DbSet<Eligibility> Eligibility => Set<Eligibility>();
    public DbSet<InitiationDecision> InitiationDecisions => Set<InitiationDecision>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Journal> Journals => Set<Journal>();
    public DbSet<JournalLine> JournalLines => Set<JournalLine>();
    public DbSet<OperationDecision> OperationDecisions => Set<OperationDecision>();
    public DbSet<BuyDecision> BuyDecisions => Set<BuyDecision>();
    public DbSet<BuyBusinessClaim> BuyBusinessClaims => Set<BuyBusinessClaim>();
    public DbSet<BuyQuoteClaim> BuyQuoteClaims => Set<BuyQuoteClaim>();
    public DbSet<GoldBacking> GoldBacking => Set<GoldBacking>();
    public DbSet<LiquidityControl> LiquidityControl => Set<LiquidityControl>();
    public DbSet<SellDecision> SellDecisions => Set<SellDecision>();
    public DbSet<SellBusinessClaim> SellBusinessClaims => Set<SellBusinessClaim>();
    public DbSet<SellQuoteClaim> SellQuoteClaims => Set<SellQuoteClaim>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Eligibility>().HasKey(x => x.CustomerId);
        model.Entity<InitiationDecision>(entity =>
        {
            entity.HasKey(x => new { x.Producer, x.CommandId });
            entity.HasIndex(x => new { x.Producer, x.PaymentIntentId }).IsUnique();
        });

        model.Entity<Account>(entity =>
        {
            entity.ToTable("Accounts", table =>
            {
                table.HasCheckConstraint("CK_Accounts_Balance", """
                    "BalanceMinor" >= 0 OR "Bucket" IN ('TradingRialClearing', 'PlatformGoldPosition')
                    """);
                table.HasCheckConstraint("CK_Accounts_Asset", """
                    "Asset" = CASE WHEN "Bucket" IN ('CustodianGold', 'PlatformGoldPosition',
                        'CustomerGoldAvailable', 'CustomerGoldReserved', 'CustomerGoldRestricted') THEN 'AU' ELSE 'IRR' END
                    AND "DebitNormal" = ("Bucket" IN ('GatewayReceivable', 'CustodianGold', 'BankCash'))
                    """);
            });
            entity.HasIndex(x => new { x.Owner, x.Bucket }).IsUnique();
            entity.Property(x => x.Asset).HasDefaultValue("IRR");
            entity.HasData(
                new Account { Id = Guid.Parse("26020000-0000-0000-0000-000000000001"), Owner = "platform", Bucket = "TradingRialClearing" },
                new Account { Id = Guid.Parse("26020000-0000-0000-0000-000000000002"), Owner = "platform", Bucket = "FeeRevenue" },
                new Account { Id = Guid.Parse("26020000-0000-0000-0000-000000000003"), Owner = "platform", Bucket = "PlatformGoldPosition", Asset = "AU" },
                new Account { Id = Guid.Parse("26030000-0000-0000-0000-000000000001"), Owner = "platform", Bucket = "BankCash", DebitNormal = true },
                new Account { Id = Guid.Parse("26030000-0000-0000-0000-000000000002"), Owner = "platform", Bucket = "PlatformCapital" });
        });

        model.Entity<Journal>().Property(x => x.Template).HasDefaultValue("F02");

        model.Entity<JournalLine>(entity =>
        {
            entity.ToTable("JournalLines", table => table.HasCheckConstraint("CK_JournalLines_Amount", "\"AmountMinor\" > 0"));
            entity.HasKey(x => new { x.JournalId, x.AccountId });
            entity.HasOne<Journal>().WithMany().HasForeignKey(x => x.JournalId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<OperationDecision>(entity =>
        {
            entity.HasKey(x => new { x.Producer, x.OperationId });
            entity.HasIndex(x => new { x.Producer, x.BusinessReference }).IsUnique();
            entity.HasIndex(x => new { x.Merchant, x.CaptureReference }).IsUnique();
            entity.HasOne<Journal>().WithMany().HasForeignKey(x => x.JournalId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<GoldBacking>(entity =>
        {
            entity.ToTable("GoldBacking", table => table.HasCheckConstraint("CK_GoldBacking_Control",
                "\"Id\" = 1 AND \"OutgoingMg\" >= 0"));
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.HasData(new GoldBacking { Id = 1 });
        });
        model.Entity<BuyDecision>(entity =>
        {
            entity.HasKey(x => x.OperationId);
            entity.HasIndex(x => x.JournalId).IsUnique();
            entity.HasOne<Journal>().WithMany().HasForeignKey(x => x.JournalId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<BuyBusinessClaim>(entity =>
        {
            entity.HasKey(x => x.OrderId);
            entity.HasOne<BuyDecision>().WithMany().HasForeignKey(x => x.OperationId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<BuyQuoteClaim>(entity =>
        {
            entity.HasKey(x => x.QuoteId);
            entity.HasOne<BuyDecision>().WithMany().HasForeignKey(x => x.OperationId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<LiquidityControl>(entity =>
        {
            entity.ToTable("LiquidityControl", table => table.HasCheckConstraint("CK_LiquidityControl_Control",
                "\"Id\" = 1 AND \"CommitmentsIrr\" >= 0 AND \"BufferIrr\" >= 0"));
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.HasData(new LiquidityControl { Id = 1 });
        });
        model.Entity<SellDecision>(entity =>
        {
            entity.HasKey(x => x.OperationId);
            entity.HasIndex(x => x.JournalId).IsUnique();
            entity.HasOne<Journal>().WithMany().HasForeignKey(x => x.JournalId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<SellBusinessClaim>(entity =>
        {
            entity.HasKey(x => x.OrderId);
            entity.HasOne<SellDecision>().WithMany().HasForeignKey(x => x.OperationId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<SellQuoteClaim>(entity =>
        {
            entity.HasKey(x => x.QuoteId);
            entity.HasOne<SellDecision>().WithMany().HasForeignKey(x => x.OperationId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

internal sealed class Eligibility
{
    public Guid CustomerId { get; init; }
    public long Version { get; set; }
    public bool IsEligible { get; set; }
}

internal sealed class InitiationDecision
{
    public required string Producer { get; init; }
    public Guid CommandId { get; init; }
    public Guid CustomerId { get; init; }
    public Guid PaymentIntentId { get; init; }
    public bool Applied { get; init; }
    public DateTimeOffset DecidedAt { get; init; }
}

internal sealed class Account
{
    public Guid Id { get; init; }
    public required string Owner { get; init; }
    public required string Bucket { get; init; }
    public long BalanceMinor { get; set; }
    public string Asset { get; init; } = "IRR";
    public bool DebitNormal { get; init; }
}

internal sealed class Journal
{
    public Guid Id { get; init; }
    public DateTimeOffset PostedAt { get; init; }
    public string Template { get; init; } = "F02";
}

internal sealed class JournalLine
{
    public Guid JournalId { get; init; }
    public Guid AccountId { get; init; }
    public bool IsDebit { get; init; }
    public long AmountMinor { get; init; }
}

internal sealed class GoldBacking
{
    public int Id { get; init; }
    public bool? IsOutage { get; init; }
    public long OutgoingMg { get; init; }
}

internal sealed class BuyDecision
{
    public Guid OperationId { get; init; }
    public Guid OrderId { get; init; }
    public Guid QuoteId { get; init; }
    public Guid CustomerId { get; init; }
    public required string Fingerprint { get; init; }
    public Guid? JournalId { get; init; }
    public string? RejectionCode { get; init; }
    public Guid? OriginalOperationId { get; init; }
    public long IncrementalUncoveredGoldMg { get; init; }
    public DateTimeOffset DecidedAt { get; init; }
}

/// <summary>Single liquidity guard row. H = active unbooked cash commitments, R = operational buffer.</summary>
internal sealed class LiquidityControl
{
    public int Id { get; init; }
    public long CommitmentsIrr { get; init; }
    public long BufferIrr { get; init; }
    public long Version { get; init; }
}

internal sealed class SellDecision
{
    public Guid OperationId { get; init; }
    public Guid OrderId { get; init; }
    public Guid QuoteId { get; init; }
    public Guid CustomerId { get; init; }
    public required string Fingerprint { get; init; }
    public Guid? JournalId { get; init; }
    public string? RejectionCode { get; init; }
    public Guid? OriginalOperationId { get; init; }
    public DateTimeOffset DecidedAt { get; init; }
}

internal sealed class SellBusinessClaim
{
    public Guid OrderId { get; init; }
    public Guid OperationId { get; init; }
}

internal sealed class SellQuoteClaim
{
    public Guid QuoteId { get; init; }
    public Guid OperationId { get; init; }
    public Guid CustomerId { get; init; }
}

internal sealed class BuyBusinessClaim
{
    public Guid OrderId { get; init; }
    public Guid OperationId { get; init; }
}

internal sealed class BuyQuoteClaim
{
    public Guid QuoteId { get; init; }
    public Guid OperationId { get; init; }
    public Guid CustomerId { get; init; }
}

internal sealed class OperationDecision
{
    public required string Producer { get; init; }
    public Guid OperationId { get; init; }
    public required string BusinessReference { get; init; }
    public string? Merchant { get; init; }
    public string? CaptureReference { get; init; }
    public required string Fingerprint { get; init; }
    public Guid CustomerId { get; init; }
    public long AmountIrr { get; init; }
    public Guid? JournalId { get; init; }
    public string? CreditedBucket { get; init; }
    public string? RejectionCode { get; init; }
    public DateTimeOffset DecidedAt { get; init; }
}
