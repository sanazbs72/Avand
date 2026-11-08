using Avand.Ledger.Api.Controls;
using Avand.Ledger.Api.Payments;
using Avand.Ledger.Api.Persistence;
using Avand.Ledger.Api.Trading;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Avand.Ledger.Api;

public static class LedgerHost
{
    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        configure?.Invoke(builder);
        builder.Services.AddDbContext<LedgerDbContext>(options =>
            options.UseNpgsql(builder.Configuration.GetConnectionString("Ledger")));
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddScoped<TopUpAuthorization>();
        builder.Services.AddScoped<GatewayCapturePosting>();
        builder.Services.AddScoped<TradingPolicy>();
        builder.Services.AddScoped<BuyGold>();
        builder.Services.AddScoped<SellGold>();
        builder.Services.AddScoped<TradingFinancialState>();
        var app = builder.Build();
        app.MapLedgerPayments();
        return app;
    }

    public static async Task MigrateAsync(string ownerConnectionString, CancellationToken ct = default,
        string? targetMigration = null)
    {
        var options = new DbContextOptionsBuilder<LedgerDbContext>().UseNpgsql(ownerConnectionString).Options;
        await using var db = new LedgerDbContext(options);
        await db.GetService<IMigrator>().MigrateAsync(targetMigration, ct);
    }
}
