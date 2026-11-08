using System.Security.Cryptography;
using System.Text;
using Avand.Application.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Avand.Application.Api.Payments;

internal sealed record StartTopUp(long AmountIrr);
internal sealed record GatewayNotice(Guid PaymentId);

internal static class TopUpEndpoints
{
    public static void MapTopUps(this WebApplication app)
    {
        var providerToken = app.Configuration["V01:CallbackToken"];
        CustomerAccess.RequireDistinctCredentials(app.Configuration);
        var wallet = app.MapCustomerGroup("/api/v1/wallet");
        wallet.MapPost("/top-ups", (StartTopUp input, HttpContext context, TopUpAdmission admission, CancellationToken ct) =>
            admission.AdmitAsync(Customer(context), context.Request.Headers["Idempotency-Key"].ToString(), input.AmountIrr, ct));
        wallet.MapGet("/top-ups/{topUpId:guid}", async (Guid topUpId, HttpContext context, ApplicationDbContext db, CancellationToken ct) =>
        {
            var customer = Customer(context);
            var topUp = await db.TopUps.AsNoTracking().SingleOrDefaultAsync(x => x.Id == topUpId && x.CustomerId == customer, ct);
            return topUp is null ? Results.NotFound() : Results.Ok(TopUpStatus.Response(topUp));
        });
        wallet.MapGet("/balance", async (HttpContext context, LedgerPaymentsClient ledger, CancellationToken ct) =>
        {
            try
            {
                var balance = await ledger.BalanceAsync(Customer(context), ct);
                return balance is null ? Results.StatusCode(503) : Results.Ok(balance);
            }
            catch (HttpRequestException)
            {
                return Results.StatusCode(503);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return Results.StatusCode(503);
            }
        });
        app.MapPost("/api/v1/payments/mock/callback", (GatewayNotice notice, HttpRequest request,
            GatewayEvidenceWorkflow evidence, CancellationToken ct) => Matches(request, providerToken)
                ? evidence.NotifyAsync(notice.PaymentId, ct)
                : Task.FromResult(Results.Unauthorized()));
    }

    private static Guid Customer(HttpContext context) => CustomerAccess.Customer(context);

    private static bool Matches(HttpRequest request, string? token) => CustomerAccess.Matches(request, token);
}
