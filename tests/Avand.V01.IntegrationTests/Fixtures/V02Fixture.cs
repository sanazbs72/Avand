using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Avand.Ledger.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Avand.V01.IntegrationTests.Fixtures;

public class V02Fixture : V01Fixture
{
    private readonly string tradingToken = Guid.NewGuid().ToString("N");
    private IConfiguration? applicationConfig;
    private long ask = 10_000_000;
    private long bid = 9_800_000;
    private string? observationTime;

    internal TestClock ApplicationClock { get; } = new();
    internal TestClock LedgerClock { get; } = new();

    protected override Task PrepareLedgerAsync() => WriteLedgerAsync("""
        DO $$
        DECLARE j uuid := gen_random_uuid();
        BEGIN
          INSERT INTO "Accounts" ("Id", "Owner", "Bucket", "BalanceMinor", "Asset", "DebitNormal")
          VALUES (gen_random_uuid(), 'mock-custodian', 'CustodianGold', 1000000, 'AU', true);
          UPDATE "Accounts" SET "BalanceMinor" = 1000000
          WHERE "Owner" = 'platform' AND "Bucket" = 'PlatformGoldPosition';
          INSERT INTO "Journals" ("Id", "PostedAt", "Template") VALUES (j, now(), 'OpeningCustody');
          INSERT INTO "JournalLines" ("JournalId", "AccountId", "IsDebit", "AmountMinor")
          SELECT j, "Id", "Bucket" = 'CustodianGold', 1000000 FROM "Accounts"
          WHERE "Bucket" IN ('CustodianGold', 'PlatformGoldPosition');
          UPDATE "GoldBacking" SET "IsOutage" = false WHERE "Id" = 1;
        END $$;
        """);

    protected override void ConfigureLedger(WebApplicationBuilder builder)
    {
        builder.Configuration["V02:LedgerToken"] = tradingToken;
        builder.Services.AddSingleton<TimeProvider>(LedgerClock);
    }

    protected override void ConfigureApplication(WebApplicationBuilder builder)
    {
        applicationConfig = builder.Configuration;
        builder.Environment.EnvironmentName = "Testing";
        builder.Configuration["V02:LedgerToken"] = tradingToken;
        builder.Configuration["V02:LedgerUrl"] = builder.Configuration["V01:LedgerUrl"];
        WritePrice();
        builder.Services.AddSingleton<TimeProvider>(ApplicationClock);
    }

    internal void SetPrice(long buy, long sell, string? observedAt)
    {
        ask = buy;
        bid = sell;
        observationTime = observedAt;
        WritePrice();
    }

    internal void ObservePrice(long buy = 10_000_000, long sell = 9_800_000) =>
        SetPrice(buy, sell, ApplicationClock.GetUtcNow().ToString("O", CultureInfo.InvariantCulture));

    internal void SetMaximumIrr(long? maximum) =>
        applicationConfig!["V02:Limits:MaximumIrr"] = maximum?.ToString(CultureInfo.InvariantCulture);

    internal static string Iso(DateTimeOffset value) => value.ToString("O", CultureInfo.InvariantCulture);

    internal void SetBuyRate(int? rate) =>
        applicationConfig!["V02:Policy:BuyFeePpm"] = rate?.ToString(CultureInfo.InvariantCulture);

    internal async Task<JsonElement> QuoteAsync(long requested, HttpClient? customer = null)
    {
        using var response = await (customer ?? Customer).PostAsJsonAsync("/api/v1/gold/buy-quotes",
            new { requestedPrincipalIrr = requested });
        Xunit.Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    internal async Task<HttpResponseMessage> ConfirmAsync(Guid quoteId, string key, HttpClient? customer = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/gold/buy-orders")
        {
            Content = JsonContent.Create(new { quoteId })
        };
        request.Headers.Add("Idempotency-Key", key);
        return await (customer ?? Customer).SendAsync(request);
    }

    internal async Task<JsonElement> AwaitBuyAsync(Guid orderId, string status, HttpClient? customer = null)
    {
        JsonElement order = default;
        await EventuallyAsync(async () =>
        {
            order = await (customer ?? Customer).GetFromJsonAsync<JsonElement>($"/api/v1/gold/buy-orders/{orderId}");
            return order.GetProperty("status").GetString() == status;
        });
        return order;
    }

    internal HttpClient TradingClient() => new() {
        BaseAddress = Ledger.BaseAddress,
        DefaultRequestHeaders = { Authorization = new AuthenticationHeaderValue("Bearer", tradingToken) }
    };

    internal async Task FundAsync(long amountIrr, HttpClient? customer = null)
    {
        var topUp = await StartAsync(customer, amountIrr: amountIrr);
        await AwaitStatusAsync(topUp, "AwaitingProvider", customer);
        Gateway.SetResult(topUp, "Ready");
        await NotifyAsync(topUp);
        await AwaitStatusAsync(topUp, "Succeeded", customer);
    }

    internal async Task<Guid> ConfirmedOrderAsync(Guid quoteId, string? key = null, HttpClient? customer = null)
    {
        using var reply = await ConfirmAsync(quoteId, key ?? Guid.NewGuid().ToString("N"), customer);
        Xunit.Assert.Equal(HttpStatusCode.Accepted, reply.StatusCode);
        return (await reply.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("orderId").GetGuid();
    }

    /// <summary>Quotes, confirms and waits for a terminal status through both real hosts.</summary>
    internal async Task<JsonElement> BuyAsync(long requestedIrr, string expected, HttpClient? customer = null)
    {
        var quote = await QuoteAsync(requestedIrr, customer);
        var order = await ConfirmedOrderAsync(quote.GetProperty("quoteId").GetGuid(), customer: customer);
        return await AwaitBuyAsync(order, expected, customer);
    }

    internal Task SetEligibleAsync(Guid customer) => WriteLedgerAsync("""
        UPDATE "Eligibility" SET "IsEligible" = true, "Version" = "Version" + 1 WHERE "CustomerId" = $1
        """, customer);

    /// <summary>Controlled custodian evidence: mode (null = unavailable) and active outgoing commitments.</summary>
    internal Task SetBackingAsync(bool? outage, long outgoingMg = 0) => WriteLedgerAsync("""
        UPDATE "GoldBacking" SET "IsOutage" = $1, "OutgoingMg" = $2 WHERE "Id" = 1
        """, outage.HasValue ? outage.Value : DBNull.Value, outgoingMg);

    internal Task<long> CustomerClaimsMgAsync() => ReadLedgerAsync("""
        SELECT coalesce(sum("BalanceMinor"), 0)::bigint FROM "Accounts"
        WHERE "Bucket" IN ('CustomerGoldAvailable', 'CustomerGoldReserved', 'CustomerGoldRestricted')
        """);

    internal Task<long> CustodyMgAsync() => ReadLedgerAsync("""
        SELECT coalesce(sum("BalanceMinor"), 0)::bigint FROM "Accounts" WHERE "Bucket" = 'CustodianGold'
        """);

    internal async Task<JsonElement> FinancialStateAsync(Guid customer)
    {
        using var trading = TradingClient();
        return await trading.GetFromJsonAsync<JsonElement>($"/internal/v1/customers/{customer}/financial-state");
    }

    /// <summary>Holds the shared backing guard from an independent session until disposed.</summary>
    internal async Task<IAsyncDisposable> HoldBackingAsync()
    {
        var connection = new NpgsqlConnection(LedgerAdmin);
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync();
        await using (var command = new NpgsqlCommand("""SELECT 1 FROM "GoldBacking" WHERE "Id" = 1 FOR UPDATE""",
            connection, transaction))
            await command.ExecuteScalarAsync();
        return new HeldGuard(connection, transaction);
    }

    /// <summary>Refuses every new Application runtime session with 53300 (role connection limit 0), ends the
    /// existing ones and drops pooled handles, so the next Application open fails. Restored on dispose.</summary>
    internal async Task<IAsyncDisposable> RefuseApplicationConnectionsAsync()
    {
        var role = new NpgsqlConnectionStringBuilder(ApplicationRuntime).Username!;
        var restore = new DeferredAction(() => ExecuteAsync(ApplicationAdmin, $"ALTER ROLE {role} CONNECTION LIMIT -1"));
        try
        {
            await ExecuteAsync(ApplicationAdmin, $"ALTER ROLE {role} CONNECTION LIMIT 0");
            await EventuallyAsync(async () =>
            {
                // A connection limit does not end existing sessions; end them, then drop the pooled handles.
                await ExecuteAsync(ApplicationAdmin,
                    "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE usename = $1", role);
                NpgsqlConnection.ClearAllPools();
                return await ScalarAsync(ApplicationAdmin,
                    "SELECT count(*) FROM pg_stat_activity WHERE usename = $1", role) == 0;
            });
            return restore;
        }
        catch
        {
            await restore.DisposeAsync();
            throw;
        }
    }

    /// <summary>Waits until <paramref name="count"/> Ledger sessions are blocked behind the held backing guard.</summary>
    internal Task AwaitBlockedLedgerSessionsAsync(int count) => EventuallyAsync(async () => await ReadLedgerAsync("""
        SELECT count(*) FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock'
        """) >= count);

    /// <summary>Builds a typed BuyGold envelope from a quote issued by the Application.</summary>
    internal static object BuyEnvelope(JsonElement quote, Guid customer, Guid order, Guid operation) => new
    {
        schemaVersion = 1, operationId = operation, operationType = "BuyGold",
        businessReference = new { kind = "GoldOrder", id = order, action = "Execute" },
        customerId = customer, actor = new { kind = "Customer", id = customer }, correlationId = order,
        payload = new
        {
            quoteSnapshot = new
            {
                side = "Buy", quoteId = quote.GetProperty("quoteId").GetGuid(),
                issuedAt = quote.GetProperty("issuedAt").GetDateTimeOffset(),
                expiresAt = quote.GetProperty("expiresAt").GetDateTimeOffset(),
                askIrrPerGram = quote.GetProperty("priceIrrPerGram").GetInt64(),
                quantityMg = quote.GetProperty("quantityMg").GetInt64(),
                principalIrr = quote.GetProperty("principalIrr").GetInt64(),
                feeRatePpm = quote.GetProperty("feeRatePpm").GetInt32(),
                feeIrr = quote.GetProperty("feeIrr").GetInt64(),
                totalDebitIrr = quote.GetProperty("totalDebitIrr").GetInt64(),
                policyVersion = "buy-v1"
            }
        }
    };

    internal async Task<JsonElement> SubmitBuyAsync(object envelope)
    {
        using var trading = TradingClient();
        using var reply = await trading.PostAsJsonAsync("/internal/v1/operations", envelope);
        Xunit.Assert.Equal(HttpStatusCode.OK, reply.StatusCode);
        return await reply.Content.ReadFromJsonAsync<JsonElement>();
    }

    internal async Task<string> FrozenRequestAsync(Guid orderId)
    {
        await using var db = new NpgsqlConnection(ApplicationAdmin);
        await db.OpenAsync();
        await using var command = new NpgsqlCommand("""SELECT "Request" FROM "BuyOrders" WHERE "Id" = $1""", db);
        command.Parameters.Add(new NpgsqlParameter { Value = orderId });
        return (string)(await command.ExecuteScalarAsync())!;
    }

    internal Task<long> ProcurementMgAsync(Guid orderId) => ReadApplicationAsync("""
        SELECT coalesce(sum("UncoveredGoldMg"), 0)::bigint FROM "ProcurementIntents" WHERE "OrderId" = $1
        """, orderId);

    internal Task<long> ProcurementCountAsync(Guid orderId) => ReadApplicationAsync("""
        SELECT count(*) FROM "ProcurementIntents" WHERE "OrderId" = $1
        """, orderId);

    internal Task<long> PostedBuyCountAsync(Guid orderId) => ReadLedgerAsync("""
        SELECT count(*) FROM "BuyDecisions" WHERE "OrderId" = $1 AND "JournalId" IS NOT NULL
        """, orderId);

    /// <summary>Fails the Ledger BuyGold transaction at commit, after the journal and decision were written.</summary>
    internal Task ArmLedgerBuyFailureAsync() => WriteLedgerAsync("""
        CREATE FUNCTION v02_fail_buy_commit() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN RAISE EXCEPTION 'V02 Ledger precommit fault'; END; $$;
        CREATE CONSTRAINT TRIGGER v02_fail_buy_commit AFTER INSERT ON "BuyDecisions"
        DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION v02_fail_buy_commit();
        """);

    internal Task DisarmLedgerBuyFailureAsync() => WriteLedgerAsync("""
        DROP TRIGGER IF EXISTS v02_fail_buy_commit ON "BuyDecisions";
        DROP FUNCTION IF EXISTS v02_fail_buy_commit();
        """);

    /// <summary>Fails Application result application after the Ledger has committed.</summary>
    internal Task ArmApplicationApplyFailureAsync() => ExecuteAsync(ApplicationAdmin, """
        CREATE FUNCTION v02_fail_apply() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN RAISE EXCEPTION 'V02 Application apply fault'; END; $$;
        CREATE TRIGGER v02_fail_apply BEFORE UPDATE ON "BuyOrders"
        FOR EACH ROW EXECUTE FUNCTION v02_fail_apply();
        """);

    internal Task DisarmApplicationApplyFailureAsync() => ExecuteAsync(ApplicationAdmin, """
        DROP TRIGGER IF EXISTS v02_fail_apply ON "BuyOrders";
        DROP FUNCTION IF EXISTS v02_fail_apply();
        """);

    internal void ResetClocks()
    {
        ApplicationClock.UseSystemTime();
        LedgerClock.UseSystemTime();
    }

    internal Task WriteLedgerAsync(string sql, params object[] values) => ExecuteAsync(LedgerAdmin, sql, values);
    internal Task WriteLedgerAsRuntimeAsync(string sql) => ExecuteAsync(LedgerRuntime, sql);
    internal Task<long> ReadLedgerAsync(string sql, params object[] values) => ScalarAsync(LedgerAdmin, sql, values);
    internal Task<long> ReadApplicationAsync(string sql, params object[] values) => ScalarAsync(ApplicationAdmin, sql, values);

    internal async Task AssertLegacyUpgradeAsync()
    {
        var database = "avand_v02_upgrade_" + Guid.NewGuid().ToString("N");
        var role = database + "_role";
        await ExecuteAsync(LedgerAdmin, $"CREATE DATABASE {database}");
        var connection = new NpgsqlConnectionStringBuilder(LedgerAdmin) { Database = database }.ConnectionString;
        try
        {
            await LedgerHost.MigrateAsync(connection, targetMigration: "20261107170000_ProtectV01Journal");
            await ExecuteAsync(connection, $"""
                CREATE ROLE {role} NOLOGIN;
                GRANT INSERT ON "Journals", "JournalLines", "OperationDecisions" TO {role};
                GRANT UPDATE ("BalanceIrr") ON "Accounts" TO {role};
                """);
            var journal = Guid.NewGuid();
            var customer = Guid.NewGuid();
            var operation = Guid.NewGuid();
            await ExecuteAsync(connection, """
                WITH accounts AS (
                    INSERT INTO "Accounts" ("Id", "Owner", "Bucket", "BalanceIrr")
                    VALUES (gen_random_uuid(), 'mock', 'GatewayReceivable', 150),
                           (gen_random_uuid(), $2, 'CustomerRialAvailable', 150)
                    RETURNING "Id", "Bucket"
                ), journal AS (
                    INSERT INTO "Journals" ("Id", "PostedAt") VALUES ($1, now()) RETURNING "Id"
                ), lines AS (
                    INSERT INTO "JournalLines" ("JournalId", "AccountId", "IsDebit", "AmountIrr")
                    SELECT journal."Id", accounts."Id", accounts."Bucket" = 'GatewayReceivable', 150 FROM accounts, journal
                )
                INSERT INTO "OperationDecisions" ("Producer", "OperationId", "BusinessReference", "Fingerprint",
                    "CustomerId", "AmountIrr", "JournalId", "CreditedBucket", "DecidedAt")
                VALUES ('Application.Payments', $3, 'legacy', 'v1:legacy', $4, 150, $1, 'CustomerRialAvailable', now());
                """, journal, customer.ToString("D"), operation, customer);
            await LedgerHost.MigrateAsync(connection);
            var preserved = await ScalarAsync(connection, """
                SELECT count(*) FROM "OperationDecisions" d JOIN "Journals" j ON j."Id" = d."JournalId"
                WHERE d."OperationId" = $1 AND j."Id" = $2 AND j."Template" = 'F02'
                  AND d."AmountIrr" = 150 AND (SELECT count(*) FROM "JournalLines" l JOIN "Accounts" a
                    ON l."AccountId" = a."Id" WHERE l."JournalId" = j."Id" AND l."AmountMinor" = 150
                    AND a."BalanceMinor" = 150 AND a."Asset" = 'IRR') = 2
                """, operation, journal);
            Xunit.Assert.Equal(1, preserved);
            var permissions = await ScalarAsync(connection, """
                SELECT CASE WHEN NOT has_table_privilege($1, 'public."Journals"', 'INSERT')
                    AND NOT has_column_privilege($1, 'public."Accounts"', 'BalanceMinor', 'UPDATE')
                    AND has_function_privilege($1, 'public.avand_lock_accounts(uuid[])', 'EXECUTE')
                    THEN 1::bigint ELSE 0::bigint END
                """, role);
            Xunit.Assert.Equal(1, permissions);
        }
        finally
        {
            await ExecuteAsync(LedgerAdmin, $"DROP DATABASE {database} WITH (FORCE)");
            await ExecuteAsync(LedgerAdmin, $"DROP ROLE IF EXISTS {role}");
        }
    }

    private void WritePrice()
    {
        if (applicationConfig is null)
            return;
        applicationConfig["V02:SyntheticPrice:Ask"] = ask.ToString(CultureInfo.InvariantCulture);
        applicationConfig["V02:SyntheticPrice:Bid"] = bid.ToString(CultureInfo.InvariantCulture);
        applicationConfig["V02:SyntheticPrice:ObservedAt"] = observationTime;
    }
}

internal sealed class HeldGuard(NpgsqlConnection connection, NpgsqlTransaction transaction) : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        await transaction.RollbackAsync();
        await transaction.DisposeAsync();
        await connection.DisposeAsync();
    }
}

internal sealed class DeferredAction(Func<Task> action) : IAsyncDisposable
{
    public async ValueTask DisposeAsync() => await action();
}

internal sealed class TestClock : TimeProvider
{
    private long ticks;

    public override DateTimeOffset GetUtcNow()
    {
        var current = Interlocked.Read(ref ticks);
        return current == 0 ? TimeProvider.System.GetUtcNow() : new DateTimeOffset(current, TimeSpan.Zero);
    }

    internal void Set(DateTimeOffset value) => Interlocked.Exchange(ref ticks, value.UtcTicks);
    internal void UseSystemTime() => Interlocked.Exchange(ref ticks, 0);
}
