using System.Net.Http.Headers;
using Avand.Application.Api.Payments;
using Avand.Application.Api.Persistence;
using Avand.Application.Api.Trading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Avand.Application.Api;

public static class ApplicationHost
{
    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        configure?.Invoke(builder);
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(builder.Configuration.GetConnectionString("Application")));
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddHttpClient<LedgerPaymentsClient>(client =>
        {
            client.BaseAddress = BaseUri(builder.Configuration, "V01:LedgerUrl");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", builder.Configuration["V01:LedgerToken"]);
            client.Timeout = TimeSpan.FromSeconds(5);
        });
        builder.Services.AddHttpClient<MockGatewayAdapter>(client =>
        {
            client.BaseAddress = BaseUri(builder.Configuration, "V01:GatewayUrl");
            client.Timeout = TimeSpan.FromSeconds(4);
        });
        builder.Services.AddScoped<TopUpAdmission>();
        builder.Services.AddScoped<GatewayEvidenceWorkflow>();
        builder.Services.AddScoped<LedgerDelivery>();
        builder.Services.AddScoped<IRecoveryHandler, PaymentRecoveryHandler>();
        builder.Services.AddHostedService<RecoveryWorker>();

        builder.Services.AddHttpClient<LedgerTradingClient>(client =>
        {
            // The URL may be shared with Payments. The Trading credential never falls back to the Payments one.
            client.BaseAddress = BaseUri(builder.Configuration,
                builder.Configuration["V02:LedgerUrl"] is null ? "V01:LedgerUrl" : "V02:LedgerUrl");
            var token = builder.Configuration["V02:LedgerToken"];
            if (!string.IsNullOrWhiteSpace(token))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.Timeout = TimeSpan.FromSeconds(5);
        });
        builder.Services.TryAddSingleton<IPairedPriceSource, SyntheticPairedPriceSource>();
        builder.Services.AddScoped<TradingPolicy>();
        builder.Services.AddScoped<BuyQuoteService>();
        builder.Services.AddScoped<BuyOrderAdmission>();
        builder.Services.AddScoped<BuyOrderDelivery>();
        builder.Services.AddHostedService<BuyRecoveryWorker>();
        builder.Services.AddScoped<SellQuoteService>();
        builder.Services.AddScoped<SellOrderAdmission>();
        builder.Services.AddScoped<SellOrderDelivery>();
        builder.Services.AddHostedService<SellRecoveryWorker>();
        var app = builder.Build();
        app.MapTopUps();
        app.MapGoldBuys();
        app.MapGoldSells();
        return app;
    }

    public static async Task MigrateAsync(string ownerConnectionString, CancellationToken ct = default)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(ownerConnectionString).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.MigrateAsync(ct);
    }

    private static Uri BaseUri(IConfiguration configuration, string key) =>
        new((configuration[key] ?? throw new InvalidOperationException($"Missing {key}.")).TrimEnd('/') + "/");
}
