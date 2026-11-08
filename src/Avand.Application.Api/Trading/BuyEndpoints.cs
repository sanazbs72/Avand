using System.Text.Json;
using System.Text.Json.Serialization;
using Avand.Application.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Avand.Application.Api.Trading;

internal sealed record BuyQuoteRequest(long RequestedPrincipalIrr);
internal sealed record BuyConfirmation(Guid QuoteId);

/// <summary>Customer V02 surface. Every reference is scoped to the authenticated owner; a foreign reference reads
/// as not found. Customer reads never trigger financial work.</summary>
internal static class BuyEndpoints
{
    private static readonly JsonSerializerOptions Strict = new(JsonSerializerOptions.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static void MapGoldBuys(this WebApplication app)
    {
        var gold = app.MapCustomerGroup("/api/v1/gold");
        gold.MapPost("/buy-quotes", (BuyQuoteRequest input, HttpContext context, BuyQuoteService quotes,
            CancellationToken ct) => quotes.IssueAsync(CustomerAccess.Customer(context), input.RequestedPrincipalIrr, ct));
        gold.MapPost("/buy-orders", async (HttpContext context, BuyOrderAdmission admission, CancellationToken ct) =>
        {
            BuyConfirmation? input;
            try
            {
                input = await JsonSerializer.DeserializeAsync<BuyConfirmation>(context.Request.Body, Strict, ct);
            }
            catch (JsonException)
            {
                return Results.BadRequest(new { code = "InvalidConfirmation" });
            }
            return input is null
                ? Results.BadRequest(new { code = "InvalidConfirmation" })
                : await admission.AdmitAsync(CustomerAccess.Customer(context),
                    context.Request.Headers["Idempotency-Key"].ToString(), input.QuoteId, ct);
        });
        gold.MapGet("/buy-orders/{orderId:guid}", async (Guid orderId, HttpContext context, ApplicationDbContext db,
            CancellationToken ct) =>
        {
            var customer = CustomerAccess.Customer(context);
            var view = await (from order in db.BuyOrders.AsNoTracking()
                              join quote in db.BuyQuotes.AsNoTracking() on order.QuoteId equals quote.Id
                              where order.Id == orderId && order.CustomerId == customer
                              select new { order, quote }).SingleOrDefaultAsync(ct);
            return view is null ? Results.NotFound() : Results.Ok(Status(view.order, view.quote));
        });
        gold.MapGet("/balances", async (HttpContext context, LedgerTradingClient ledger, CancellationToken ct) =>
        {
            var state = await ledger.FinancialStateAsync(CustomerAccess.Customer(context), ct);
            return state is null
                ? Results.Json(new { code = "BalanceUnavailable", retryable = true }, statusCode: StatusCodes.Status503ServiceUnavailable)
                : Results.Ok(new { availableIrr = state.AvailableIrr, availableGoldMg = state.AvailableGoldMg });
        });
    }

    private static object Status(BuyOrder order, BuyQuote quote) => new
    {
        orderId = order.Id,
        quoteId = quote.Id,
        status = order.Status,
        reasonCode = order.ReasonCode,
        message = Message(order),
        priceIrrPerGram = quote.AskIrrPerGram,
        quote.QuantityMg,
        quote.PrincipalIrr,
        quote.FeeRatePpm,
        quote.FeeIrr,
        quote.TotalDebitIrr,
        quote.RequestedPrincipalIrr,
        quote.IssuedAt,
        quote.ExpiresAt,
        order.DecidedAt,
        // An expired quote is never repriced. The customer requests a new quote and confirms it with a new key.
        refresh = order is { Status: "Rejected", ReasonCode: "QuoteExpired" }
            ? new
            {
                action = "RequestNewQuote",
                method = "POST",
                href = "/api/v1/gold/buy-quotes",
                requestedPrincipalIrr = quote.RequestedPrincipalIrr
            }
            : null
    };

    private static string Message(BuyOrder order) => (order.Status, order.ReasonCode) switch
    {
        ("Completed", _) => "خرید ثبت شد و طلا به موجودی شما اضافه شد.",
        ("Rejected", "QuoteExpired") => "مهلت قیمت پیشنهادی پیش از ثبت به پایان رسید. برای خرید، قیمت جدید دریافت و تأیید کنید.",
        ("Rejected", "InsufficientRialBalance") => "موجودی قابل‌مصرف کیف پول برای این خرید کافی نیست.",
        ("Rejected", "InsufficientBacking" or "BackingEvidenceUnavailable") => "در حال حاضر ظرفیت خرید این مقدار طلا وجود ندارد.",
        ("Rejected", "CustomerNotEligible") => "این حساب در حال حاضر مجاز به خرید نیست.",
        ("Rejected", "OrderOutOfBounds") => "مبلغ سفارش خارج از حدود مجاز است.",
        ("Rejected", _) => "سفارش خرید ثبت نشد.",
        ("FinancialOutcomeUnknown", _) => "نتیجهٔ خرید در حال پیگیری است.",
        _ => "سفارش خرید در حال پردازش است."
    };
}
