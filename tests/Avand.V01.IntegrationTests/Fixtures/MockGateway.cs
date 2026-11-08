using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Avand.V01.IntegrationTests.Fixtures;

internal sealed class MockGateway : IAsyncDisposable
{
    private readonly object sync = new();
    private readonly Dictionary<Guid, Payment> payments = [];
    private WebApplication? server;
    private DateTimeOffset now = new(2026, 11, 7, 8, 0, 0, TimeSpan.Zero);

    public Uri BaseAddress => new(server!.Urls.Single());

    public async Task StartAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        server = builder.Build();
        server.MapPost("/sessions", (SessionRequest request) => Create(request));
        server.MapPost("/payments/{id:guid}/verify", (Guid id) => Resolve(id, verify: true));
        server.MapGet("/payments/{id:guid}", async (Guid id, CancellationToken ct) =>
        {
            if (IsTimeout(id))
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return Resolve(id, verify: false);
        });
        await server.StartAsync();
    }

    public void Advance(TimeSpan duration)
    {
        lock (sync)
            now += duration;
    }

    public void SetResult(Guid paymentId, string state, long? reportedAmount = null, Guid? reportedPayment = null,
        bool missingCaptureReference = false)
    {
        lock (sync)
        {
            var payment = payments[paymentId];
            payment.State = state;
            payment.ReportedAmount = reportedAmount;
            payment.ReportedPayment = reportedPayment;
            payment.MissingCaptureReference = missingCaptureReference;
        }
    }

    public int CallCount(Guid paymentId)
    {
        lock (sync)
            return payments[paymentId].Calls;
    }

    public string? LastOutcome(Guid paymentId)
    {
        lock (sync)
            return payments[paymentId].LastOutcome;
    }

    private bool IsTimeout(Guid id)
    {
        lock (sync)
        {
            if (!payments.TryGetValue(id, out var payment) || payment.State != "Timeout")
                return false;
            payment.Calls++;
            return true;
        }
    }

    private IResult Create(SessionRequest request)
    {
        lock (sync)
        {
            if (!payments.TryGetValue(request.RequestReference, out var payment))
            {
                payment = new Payment(request.RequestReference, request.AmountIrr, now);
                payments.Add(payment.Id, payment);
            }

            if (payment.AmountIrr != request.AmountIrr)
                return Results.Conflict();

            return Results.Ok(new { paymentId = payment.Id, payment.CreatedAt, continuation = $"/mock/pay/{payment.Id}" });
        }
    }

    private IResult Resolve(Guid id, bool verify)
    {
        lock (sync)
        {
            if (!payments.TryGetValue(id, out var payment))
                return Results.NotFound();
            payment.Calls++;
            if (payment.State == "Unavailable")
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

            if (verify && payment.State is "Ready" or "Unknown")
            {
                if (now - payment.CreatedAt > TimeSpan.FromMinutes(15))
                    payment.State = "Expired";
                else if (payment.State == "Ready")
                    payment.State = "Captured";
            }

            var outcome = payment.State == "Ready" ? "Unknown" : payment.State;
            payment.LastOutcome = outcome;
            return Results.Ok(new
            {
                paymentId = payment.ReportedPayment ?? id,
                outcome,
                captureReference = outcome == "Captured" && !payment.MissingCaptureReference ? $"capture-{id:N}" : null,
                amountIrr = outcome == "Captured" ? payment.ReportedAmount ?? payment.AmountIrr : (long?)null
            });
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (server is not null)
            await server.DisposeAsync();
    }

    private sealed record SessionRequest(Guid RequestReference, long AmountIrr);

    private sealed record Payment(Guid Id, long AmountIrr, DateTimeOffset CreatedAt)
    {
        public string State { get; set; } = "Unknown";
        public Guid? ReportedPayment { get; set; }
        public long? ReportedAmount { get; set; }
        public string? LastOutcome { get; set; }
        public bool MissingCaptureReference { get; set; }
        public int Calls { get; set; }
    }
}
