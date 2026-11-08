using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avand.Ledger.Api.Controls;
using Avand.Ledger.Api.Trading;

namespace Avand.Ledger.Api.Payments;

/// <summary>Internal Ledger routes. Each route is registered once and dispatches by the authenticated producer
/// and the operation types that producer is permitted to submit.</summary>
internal static class LedgerPaymentEndpoints
{
    internal const string Producer = "Application.Payments";
    private const string Capture = "ApplyGatewayPaymentEvidence";
    private const string Buy = "BuyGold";
    private const string Sell = SellGold.OperationType;

    private static readonly JsonSerializerOptions Strict = new(JsonSerializerOptions.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static void MapLedgerPayments(this WebApplication app)
    {
        var producers = new (string Producer, string? Credential)[]
        {
            (Producer, app.Configuration["V01:LedgerToken"]),
            (BuyGold.Producer, app.Configuration["V02:LedgerToken"])
        }.Where(x => !string.IsNullOrWhiteSpace(x.Credential)).ToArray();
        if (producers.Select(x => x.Credential).Distinct(StringComparer.Ordinal).Count() != producers.Length)
            throw new InvalidOperationException("Payments and Trading Ledger credentials must be distinct.");

        var routes = app.MapGroup("/internal/v1").AddEndpointFilter(async (context, next) =>
        {
            var supplied = context.HttpContext.Request.Headers.Authorization.ToString();
            var producer = producers.FirstOrDefault(x => Matches(supplied, "Bearer " + x.Credential)).Producer;
            if (producer is null)
                return Results.Unauthorized();

            context.HttpContext.Items[typeof(LedgerPaymentEndpoints)] = producer;
            return await next(context);
        });

        routes.MapPost("/control-commands", (ControlRequest request, HttpContext context, TopUpAuthorization controls,
            CancellationToken ct) => Is(context, Producer) ? controls.ExecuteAsync(Producer, request, ct) : Denied());
        routes.MapGet("/control-commands/{commandId:guid}", (Guid commandId, HttpContext context,
            TopUpAuthorization controls, CancellationToken ct) =>
            Is(context, Producer) ? controls.ReadAsync(Producer, commandId, ct) : Denied());
        routes.MapPost("/operations", DispatchAsync);
        routes.MapGet("/operations/{operationId:guid}", (Guid operationId, HttpContext context, IServiceProvider services,
            CancellationToken ct) => Is(context, Producer)
                ? services.GetRequiredService<GatewayCapturePosting>().ReadAsync(Producer, operationId, ct)
                : ReadTradingAsync(services, operationId, ct));
        routes.MapGet("/customers/{customerId:guid}/balances", (Guid customerId, HttpContext context,
            GatewayCapturePosting payments, CancellationToken ct) =>
            Is(context, Producer) ? payments.BalanceAsync(customerId, ct) : Denied());
        routes.MapGet("/customers/{customerId:guid}/financial-state", (Guid customerId, HttpContext context,
            TradingFinancialState state, CancellationToken ct) =>
            Is(context, BuyGold.Producer) ? state.ReadAsync(customerId, ct) : Denied());
    }

    private static async Task<IResult> DispatchAsync(HttpContext context, IServiceProvider services, CancellationToken ct)
    {
        JsonElement body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<JsonElement>(context.Request.Body, JsonSerializerOptions.Web, ct);
        }
        catch (JsonException)
        {
            return Results.BadRequest();
        }

        var type = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("operationType", out var value) &&
            value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        var producer = (string)context.Items[typeof(LedgerPaymentEndpoints)]!;
        switch (producer, type)
        {
            case (Producer, Capture):
                var capture = Deserialize<FinancialRequest>(body, JsonSerializerOptions.Web);
                return capture is null
                    ? Results.BadRequest()
                    : await services.GetRequiredService<GatewayCapturePosting>().ExecuteAsync(Producer, capture, ct);
            case (BuyGold.Producer, Buy):
                var buy = Deserialize<BuyRequest>(body, Strict);
                return buy is null ? Results.BadRequest() : await services.GetRequiredService<BuyGold>().ExecuteAsync(buy, ct);
            case (BuyGold.Producer, Sell):
                var sell = Deserialize<SellRequest>(body, Strict);
                return sell is null ? Results.BadRequest() : await services.GetRequiredService<SellGold>().ExecuteAsync(sell, ct);
            case (_, Capture or Buy or Sell):
                return await Denied();
            default:
                return Results.BadRequest(new { code = "UnsupportedOperationType" });
        }
    }

    /// <summary>Trading-scoped inquiry across its own operation types. Operation IDs are permanent and unique per
    /// order, so at most one of the sell and buy stores holds a given ID.</summary>
    private static async Task<IResult> ReadTradingAsync(IServiceProvider services, Guid operationId, CancellationToken ct)
    {
        var sale = await services.GetRequiredService<SellGold>().FindAsync(operationId, ct);
        return sale is not null ? Results.Ok(sale) : await services.GetRequiredService<BuyGold>().ReadAsync(operationId, ct);
    }

    private static T? Deserialize<T>(JsonElement body, JsonSerializerOptions options) where T : class
    {
        try
        {
            return body.Deserialize<T>(options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool Is(HttpContext context, string producer) =>
        (string?)context.Items[typeof(LedgerPaymentEndpoints)] == producer;

    private static Task<IResult> Denied() =>
        Task.FromResult(Results.Json(new { code = "OperationNotPermitted" }, statusCode: StatusCodes.Status403Forbidden));

    private static bool Matches(string supplied, string expected) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(expected));
}
