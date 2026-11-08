using System.Text.Json;
using System.Text.Json.Serialization;
using Avand.Application.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Avand.Application.Api.Trading;

internal sealed record SellQuoteRequest(long? QuantityMg, long? RequestedGrossIrr);
internal sealed record SellConfirmation(Guid QuoteId);

/// <summary>Customer V03 surface. Every reference is scoped to the authenticated owner; a foreign reference reads as
/// not found. Customer reads never trigger financial work. Platform liquidity is never disclosed.</summary>
internal static class SellEndpoints
{
    private static readonly JsonSerializerOptions Strict = new(JsonSerializerOptions.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static void MapGoldSells(this WebApplication app)
    {
        var gold = app.MapCustomerGroup("/api/v1/gold");
        gold.MapPost("/sell-quotes", async (HttpContext context, SellQuoteService quotes, CancellationToken ct) =>
        {
            var input = await ReadAsync<SellQuoteRequest>(context, ct);
            return input is null
                ? Results.BadRequest(new { code = "InvalidQuoteRequest" })
                : await quotes.IssueAsync(CustomerAccess.Customer(context), input.QuantityMg, input.RequestedGrossIrr, ct);
        });
        gold.MapPost("/sell-orders", async (HttpContext context, SellOrderAdmission admission, CancellationToken ct) =>
        {
            var input = await ReadAsync<SellConfirmation>(context, ct);
            return input is null
                ? Results.BadRequest(new { code = "InvalidConfirmation" })
                : await admission.AdmitAsync(CustomerAccess.Customer(context),
                    context.Request.Headers["Idempotency-Key"].ToString(), input.QuoteId, ct);
        });
        gold.MapGet("/sell-orders/{orderId:guid}", async (Guid orderId, HttpContext context, ApplicationDbContext db,
            CancellationToken ct) =>
        {
            var customer = CustomerAccess.Customer(context);
            var view = await (from order in db.SellOrders.AsNoTracking()
                              join quote in db.SellQuotes.AsNoTracking() on order.QuoteId equals quote.Id
                              where order.Id == orderId && order.CustomerId == customer
                              select new { order, quote }).SingleOrDefaultAsync(ct);
            return view is null ? Results.NotFound() : Results.Ok(Status(view.order, view.quote));
        });
    }

    private static async Task<T?> ReadAsync<T>(HttpContext context, CancellationToken ct) where T : class
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<T>(context.Request.Body, Strict, ct);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static object Status(SellOrder order, SellQuote quote) => new
    {
        orderId = order.Id,
        quoteId = quote.Id,
        status = order.Status,
        reasonCode = CustomerReason(order.ReasonCode),
        message = Message(order),
        inputMode = quote.InputMode,
        inputValue = quote.InputValue,
        priceIrrPerGram = quote.BidIrrPerGram,
        quote.QuantityMg,
        quote.GrossIrr,
        quote.FeeRatePpm,
        quote.FeeIrr,
        quote.NetIrr,
        quote.IssuedAt,
        quote.ExpiresAt,
        order.DecidedAt,
        // An expired quote is never repriced: the refresh repeats the original input and needs a new confirmation.
        refresh = order is { Status: "Rejected", ReasonCode: "QuoteExpired" }
            ? new
            {
                action = "RequestNewQuote",
                method = "POST",
                href = "/api/v1/gold/sell-quotes",
                quantityMg = quote.InputMode == "Weight" ? quote.InputValue : (long?)null,
                requestedGrossIrr = quote.InputMode == "GrossIrr" ? quote.InputValue : (long?)null
            }
            : null
    };

    /// <summary>Liquidity shortfalls are shown as a temporary sales outage, without platform detail.</summary>
    private static string? CustomerReason(string? reason) =>
        reason == "InsufficientLiquidity" ? "SalesTemporarilyUnavailable" : reason;

    private static string Message(SellOrder order) => (order.Status, order.ReasonCode) switch
    {
        ("Completed", _) => "فروش ثبت شد و مبلغ خالص به کیف پول شما اضافه شد.",
        ("Rejected", "QuoteExpired") => "مهلت قیمت پیشنهادی پیش از ثبت به پایان رسید. برای فروش، قیمت جدید دریافت و تأیید کنید.",
        ("Rejected", "InsufficientGold") => "موجودی طلای قابل فروش برای این سفارش کافی نیست.",
        ("Rejected", "InsufficientLiquidity") => "فروش طلا در حال حاضر موقتاً در دسترس نیست. لطفاً بعداً تلاش کنید.",
        ("Rejected", "CustomerNotEligible") => "این حساب در حال حاضر مجاز به فروش نیست.",
        ("Rejected", "OrderOutOfBounds") => "مبلغ سفارش خارج از حدود مجاز است.",
        ("Rejected", _) => "سفارش فروش ثبت نشد.",
        ("FinancialOutcomeUnknown", _) => "نتیجهٔ فروش در حال پیگیری است.",
        _ => "سفارش فروش در حال پردازش است."
    };
}
