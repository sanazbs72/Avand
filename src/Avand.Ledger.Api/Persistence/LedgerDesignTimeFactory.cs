using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Avand.Ledger.Api.Persistence;

internal sealed class LedgerDesignTimeFactory : IDesignTimeDbContextFactory<LedgerDbContext>
{
    public LedgerDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("AVAND_LEDGER_DATABASE")
            ?? "Host=localhost;Database=avand_ledger";
        var options = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new LedgerDbContext(options);
    }
}
