using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Avand.Application.Api;
using Avand.Ledger.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Avand.V01.IntegrationTests.Fixtures;

public class V01Fixture : IAsyncLifetime
{
    internal const int RuntimePoolSize = 32;
    private readonly PostgreSqlFixture postgres = new();
    private readonly string databaseSuffix = Guid.NewGuid().ToString("N")[..12];
    private string ApplicationDatabase => "avand_application_" + databaseSuffix;
    private string LedgerDatabase => "avand_ledger_" + databaseSuffix;
    private string ApplicationRole => "v01_application_" + databaseSuffix;
    private string LedgerRole => "v01_ledger_" + databaseSuffix;
    private readonly string ledgerToken = Guid.NewGuid().ToString("N");
    private readonly string callbackToken = Guid.NewGuid().ToString("N");
    private readonly string customerToken = Guid.NewGuid().ToString("N");
    private readonly string otherToken = Guid.NewGuid().ToString("N");
    private readonly string restrictedToken = Guid.NewGuid().ToString("N");
    private WebApplication? applicationHost;
    private WebApplication? ledgerHost;
    private string ledgerAdmin = null!;
    private string applicationAdmin = null!;
    private string ledgerRuntime = null!;
    private string applicationRuntime = null!;
    private int dropNextResponse;
    private int blockPosting;
    private int observedDroppedResponse;
    private int blockOperations;
    private int blockAfterDrop;

    internal MockGateway Gateway { get; } = new();
    internal Guid CustomerId { get; } = Guid.NewGuid();
    internal Guid OtherId { get; } = Guid.NewGuid();
    internal Guid RestrictedId { get; } = Guid.NewGuid();
    internal bool DroppedResponse => Volatile.Read(ref observedDroppedResponse) != 0;
    internal void DropNextPostingResponse() => Interlocked.Exchange(ref dropNextResponse, 1);
    internal void BlockPosting(bool block) => Interlocked.Exchange(ref blockPosting, block ? 1 : 0);
    /// <summary>Fails every Application call to the Ledger operation routes, including inquiry.</summary>
    internal void BlockOperations(bool block) => Interlocked.Exchange(ref blockOperations, block ? 1 : 0);
    /// <summary>Drops the next successful posting response, then blocks all operation calls until released.</summary>
    internal void DropNextPostingResponseThenBlock()
    {
        Interlocked.Exchange(ref observedDroppedResponse, 0);
        Interlocked.Exchange(ref blockAfterDrop, 1);
        Interlocked.Exchange(ref dropNextResponse, 1);
    }
    public HttpClient Ledger { get; private set; } = null!;
    public HttpClient Untrusted { get; private set; } = null!;
    internal HttpClient Customer { get; private set; } = null!;
    internal HttpClient Other { get; private set; } = null!;
    internal HttpClient Restricted { get; private set; } = null!;
    internal HttpClient Callback { get; private set; } = null!;
    protected string LedgerAdmin => ledgerAdmin;
    protected string LedgerRuntime => ledgerRuntime;
    protected string ApplicationAdmin => applicationAdmin;
    protected string ApplicationRuntime => applicationRuntime;
    protected virtual void ConfigureLedger(WebApplicationBuilder builder) { }
    protected virtual void ConfigureApplication(WebApplicationBuilder builder) { }
    protected virtual Task PrepareLedgerAsync() => Task.CompletedTask;

    public async Task InitializeAsync()
    {
        await postgres.InitializeAsync();
        await ExecuteAsync(postgres.AdminConnectionString, $"CREATE DATABASE {ApplicationDatabase}");
        applicationAdmin = Connection(ApplicationDatabase);
        await ExecuteAsync(postgres.AdminConnectionString, $"CREATE DATABASE {LedgerDatabase}");
        ledgerAdmin = Connection(LedgerDatabase);
        await ApplicationHost.MigrateAsync(applicationAdmin);
        await LedgerHost.MigrateAsync(ledgerAdmin);

        applicationRuntime = await ConfigureRoleAsync(applicationAdmin, ApplicationDatabase, ApplicationRole, $"""
            GRANT SELECT, INSERT, UPDATE ON "TopUps" TO {ApplicationRole};
            GRANT SELECT, INSERT ON "GatewayEvidence" TO {ApplicationRole};
            GRANT SELECT, INSERT ON "RecoveryWork" TO {ApplicationRole};
            GRANT UPDATE ("DueAt", "LeasedUntil", "Attempts", "Version") ON "RecoveryWork" TO {ApplicationRole};
            GRANT SELECT, INSERT ON "BuyQuotes", "BuyOrders", "BuyRecoveryWork", "ProcurementIntents" TO {ApplicationRole};
            GRANT UPDATE ("Status", "ReasonCode", "JournalId", "DecidedAt", "IncrementalUncoveredGoldMg", "Version")
                ON "BuyOrders" TO {ApplicationRole};
            GRANT UPDATE ("DueAt", "LeasedUntil", "Attempts", "Version") ON "BuyRecoveryWork" TO {ApplicationRole};
            GRANT SELECT, INSERT ON "SellQuotes", "SellOrders", "SellRecoveryWork" TO {ApplicationRole};
            GRANT UPDATE ("Status", "ReasonCode", "JournalId", "DecidedAt", "Version") ON "SellOrders" TO {ApplicationRole};
            GRANT UPDATE ("DueAt", "LeasedUntil", "Attempts", "Version") ON "SellRecoveryWork" TO {ApplicationRole};
            """);
        ledgerRuntime = await ConfigureRoleAsync(ledgerAdmin, LedgerDatabase, LedgerRole, $"""
            GRANT SELECT ON "Eligibility", "Accounts", "Journals", "JournalLines", "InitiationDecisions",
                "OperationDecisions", "GoldBacking", "BuyDecisions", "BuyBusinessClaims", "BuyQuoteClaims",
                "LiquidityControl", "SellDecisions", "SellBusinessClaims", "SellQuoteClaims" TO {LedgerRole};
            GRANT UPDATE ("CustomerId") ON "Eligibility" TO {LedgerRole};
            GRANT INSERT ON "InitiationDecisions", "OperationDecisions", "BuyDecisions", "BuyBusinessClaims", "BuyQuoteClaims",
                "SellDecisions", "SellBusinessClaims", "SellQuoteClaims" TO {LedgerRole};
            GRANT EXECUTE ON FUNCTION public.avand_lock_accounts(uuid[]), public.avand_lock_backing(),
                public.avand_post_journal(uuid, timestamptz, text, uuid[], boolean[], bigint[]),
                public.avand_open_gold_account(uuid), public.avand_lock_liquidity(),
                public.avand_free_liquidity() TO {LedgerRole};
            """);
        await ExecuteAsync(ledgerAdmin, """
            INSERT INTO "Accounts" ("Id", "Owner", "Bucket", "BalanceMinor", "DebitNormal")
            VALUES (gen_random_uuid(), 'mock', 'GatewayReceivable', 0, true)
            """);
        await SeedAsync(CustomerId, true);
        await SeedAsync(OtherId, true);
        await SeedAsync(RestrictedId, false);
        await PrepareLedgerAsync();
        await Gateway.StartAsync();
        ledgerHost = LedgerHost.Build([], builder =>
        {
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Ledger"] = ledgerRuntime,
                ["V01:LedgerToken"] = ledgerToken
            });
            ConfigureLedger(builder);
        });
        await ledgerHost.StartAsync();
        Ledger = Client(ledgerHost, ledgerToken);
        Untrusted = Client(ledgerHost, null);
        await StartApplicationAsync();
    }

    internal async Task RestartApplicationAsync()
    {
        await DisposeApplicationAsync();
        await StartApplicationAsync();
    }

    private async Task StartApplicationAsync()
    {
        applicationHost = ApplicationHost.Build([], builder =>
        {
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Application"] = applicationRuntime,
                ["V01:LedgerUrl"] = ledgerHost!.Urls.Single(),
                ["V01:LedgerToken"] = ledgerToken,
                ["V01:GatewayUrl"] = Gateway.BaseAddress.ToString(),
                ["V01:CallbackToken"] = callbackToken,
                [$"V01:Customers:{CustomerId:D}"] = customerToken,
                [$"V01:Customers:{OtherId:D}"] = otherToken,
                [$"V01:Customers:{RestrictedId:D}"] = restrictedToken
            });
            builder.Services.ConfigureHttpClientDefaults(client => client.AddHttpMessageHandler(() => new LedgerFaults(this)));
            ConfigureApplication(builder);
        });
        await applicationHost.StartAsync();
        Customer = Client(applicationHost, customerToken);
        Other = Client(applicationHost, otherToken);
        Restricted = Client(applicationHost, restrictedToken);
        Callback = Client(applicationHost, callbackToken);
    }

    private async Task<string> ConfigureRoleAsync(string admin, string db, string role, string grants)
    {
        var password = Guid.NewGuid().ToString("N");
        await ExecuteAsync(admin, $"""
            CREATE ROLE {role} LOGIN PASSWORD '{password}';
            REVOKE CONNECT ON DATABASE {db} FROM PUBLIC;
            GRANT CONNECT ON DATABASE {db} TO {role};
            GRANT USAGE ON SCHEMA public TO {role};
            {grants}
            """);
        // Both runtime pools share one server (max_connections 100, 3 reserved for superusers) with the admin
        // pools. Npgsql's default 100 per pool lets a single burst overrun it with 53300; budget the pools so a
        // burst queues inside the pool, as a deployment must.
        return new NpgsqlConnectionStringBuilder(admin)
            { Username = role, Password = password, MaxPoolSize = RuntimePoolSize }.ConnectionString;
    }

    public async Task<Guid> SeedCustomerAsync()
    {
        var customer = Guid.NewGuid();
        await SeedAsync(customer, true);
        return customer;
    }

    private Task SeedAsync(Guid id, bool eligible) => ExecuteAsync(ledgerAdmin, """
        WITH seeded AS (
            INSERT INTO "Eligibility" ("CustomerId", "IsEligible", "Version") VALUES ($1, $2, 1)
        )
        INSERT INTO "Accounts" ("Id", "Owner", "Bucket", "BalanceMinor") VALUES
          (gen_random_uuid(), $3, 'CustomerRialAvailable', 0),
          (gen_random_uuid(), $3, 'CustomerRialRestricted', 0)
        """, id, eligible, id.ToString("D"));

    internal Task RestrictAsync(Guid customer) => ExecuteAsync(ledgerAdmin, """
        UPDATE "Eligibility" SET "IsEligible" = false, "Version" = "Version" + 1 WHERE "CustomerId" = $1
        """, customer);

    public async Task AuthorizeAsync(Guid customer, Guid topUp)
    {
        using var reply = await Ledger.PostAsJsonAsync("/internal/v1/control-commands", new
        {
            schemaVersion = 1, commandId = topUp, commandType = "AuthorizeTopUpInitiation",
            customerId = customer, payload = new { paymentIntentId = topUp }
        });
        reply.EnsureSuccessStatusCode();
        var body = await reply.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Applied", body.GetProperty("status").GetString());
    }

    public object Capture(Guid customer, Guid payment, Guid operation, long amount) => new
    {
        schemaVersion = 1, operationId = operation, operationType = "ApplyGatewayPaymentEvidence",
        businessReference = new { kind = "WalletTopUp", id = payment, action = "Capture" },
        customerId = customer, actor = new { kind = "Customer", id = customer }, correlationId = payment,
        payload = new
        {
            paymentId = payment, captureRef = $"capture-{payment:N}",
            capturedIrr = amount.ToString(CultureInfo.InvariantCulture), evidenceBatchId = payment,
            confirmedReversals = Array.Empty<object>(), evidenceRef = new { id = payment, digest = new string('a', 64) }
        }
    };

    public async Task<long> AvailableAsync(Guid customer)
    {
        var result = await Ledger.GetFromJsonAsync<JsonElement>($"/internal/v1/customers/{customer}/balances");
        return result.GetProperty("availableIrr").GetInt64();
    }

    internal Task<long> RestrictedAsync(Guid customer) => ScalarAsync(ledgerAdmin, """
        SELECT "BalanceMinor" FROM "Accounts" WHERE "Owner" = $1 AND "Bucket" = 'CustomerRialRestricted'
        """, customer.ToString("D"));

    public async Task<int> JournalLineCountAsync(Guid journal) => checked((int)await ScalarAsync(ledgerAdmin,
        """SELECT count(*) FROM "JournalLines" WHERE "JournalId" = $1""", journal));

    internal Task<long> PostingCountAsync(Guid topUp) => ScalarAsync(ledgerAdmin, """
        SELECT count(*) FROM "OperationDecisions" WHERE "BusinessReference" = $1 AND "JournalId" IS NOT NULL
        """, $"WalletTopUp/{topUp:D}/Capture");

    internal Task<long> DecisionCountAsync(Guid topUp) => ScalarAsync(ledgerAdmin, """
        SELECT count(*) FROM "OperationDecisions" WHERE "BusinessReference" = $1
        """, $"WalletTopUp/{topUp:D}/Capture");

    internal Task<long> JournalCountAsync() => ScalarAsync(ledgerAdmin, """SELECT count(*) FROM "Journals" """);

    internal Task<long> LedgerAttemptsAsync(Guid topUp) => ScalarAsync(applicationAdmin, """
        SELECT coalesce(max("Attempts"), 0)::bigint FROM "RecoveryWork" WHERE "TopUpId" = $1 AND "Kind" = 'Ledger'
        """, topUp);

    internal Task ArmCommitFailureAsync() => ExecuteAsync(ledgerAdmin, """
        CREATE FUNCTION v01_fail_commit() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN RAISE EXCEPTION 'V01 precommit fault'; END; $$;
        CREATE CONSTRAINT TRIGGER v01_fail_commit AFTER INSERT ON "OperationDecisions"
        DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION v01_fail_commit();
        """);

    internal Task DisarmCommitFailureAsync() => ExecuteAsync(ledgerAdmin, """
        DROP TRIGGER IF EXISTS v01_fail_commit ON "OperationDecisions";
        DROP FUNCTION IF EXISTS v01_fail_commit();
        """);

    internal async Task AssertRoleIsolationAsync()
    {
        var foreign = new NpgsqlConnectionStringBuilder(applicationRuntime) { Database = LedgerDatabase };
        var denied = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(foreign.ConnectionString, "SELECT 1"));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
        var journal = await Assert.ThrowsAsync<PostgresException>(() =>
            ExecuteAsync(ledgerRuntime, """DELETE FROM "JournalLines" WHERE false"""));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, journal.SqlState);
    }

    internal async Task AssertProjectionGuardAsync(Guid customer)
    {
        const string update = """
            UPDATE "Accounts" SET "BalanceMinor" = "BalanceMinor" + 1
            WHERE "Owner" = $1 AND "Bucket" = 'CustomerRialAvailable'
            """;
        var denied = await Assert.ThrowsAsync<PostgresException>(() =>
            ExecuteAsync(ledgerRuntime, update, customer.ToString("D")));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
        var failure = await Assert.ThrowsAsync<PostgresException>(() =>
            ExecuteAsync(ledgerAdmin, update, customer.ToString("D")));
        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
    }

    internal async Task<Guid> StartAsync(HttpClient? client = null, string? key = null, long amountIrr = 100)
    {
        client ??= Customer;
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/wallet/top-ups")
        {
            Content = JsonContent.Create(new { amountIrr })
        };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString("N"));
        using var reply = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Accepted, reply.StatusCode);
        var body = await reply.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("topUpId").GetGuid();
    }

    internal async Task<JsonElement> AwaitStatusAsync(Guid id, string status, HttpClient? client = null)
    {
        client ??= Customer;
        JsonElement result = default;
        await EventuallyAsync(async () =>
        {
            result = await client.GetFromJsonAsync<JsonElement>($"/api/v1/wallet/top-ups/{id}");
            return result.GetProperty("status").GetString() == status;
        });
        return result;
    }

    internal static async Task EventuallyAsync(Func<Task<bool>> check)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        while (!await check())
            await Task.Delay(150, timeout.Token);
    }

    internal async Task NotifyAsync(Guid payment)
    {
        using var reply = await Callback.PostAsJsonAsync("/api/v1/payments/mock/callback", new { paymentId = payment });
        Assert.Equal(HttpStatusCode.Accepted, reply.StatusCode);
    }

    private string Connection(string db) => new NpgsqlConnectionStringBuilder(postgres.AdminConnectionString)
        { Database = db }.ConnectionString;

    private static HttpClient Client(WebApplication host, string? token)
    {
        var client = new HttpClient { BaseAddress = new Uri(host.Urls.Single()), Timeout = TimeSpan.FromSeconds(10) };
        if (token is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    protected static async Task ExecuteAsync(string connection, string sql, params object[] arguments)
    {
        await using var db = new NpgsqlConnection(connection);
        await db.OpenAsync();
        await using var command = Command(db, sql, arguments);
        await command.ExecuteNonQueryAsync();
    }

    protected static async Task<long> ScalarAsync(string connection, string sql, params object[] arguments)
    {
        await using var db = new NpgsqlConnection(connection);
        await db.OpenAsync();
        await using var command = Command(db, sql, arguments);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static NpgsqlCommand Command(NpgsqlConnection db, string sql, object[] arguments)
    {
        var command = new NpgsqlCommand(sql, db);
        foreach (var value in arguments)
            command.Parameters.Add(new NpgsqlParameter { Value = value });
        return command;
    }

    private async Task DisposeApplicationAsync()
    {
        if (applicationHost is null) return;
        Customer.Dispose();
        Other.Dispose();
        Restricted.Dispose();
        Callback.Dispose();
        await applicationHost.StopAsync();
        await applicationHost.DisposeAsync();
        applicationHost = null;
    }

    public async Task DisposeAsync()
    {
        await DisposeApplicationAsync();
        Ledger?.Dispose();
        Untrusted?.Dispose();
        if (ledgerHost is not null)
            await ledgerHost.DisposeAsync();
        await Gateway.DisposeAsync();
        try
        {
            if (applicationAdmin is not null)
                await ExecuteAsync(postgres.AdminConnectionString, $"DROP DATABASE {ApplicationDatabase} WITH (FORCE)");
            if (ledgerAdmin is not null)
                await ExecuteAsync(postgres.AdminConnectionString, $"DROP DATABASE {LedgerDatabase} WITH (FORCE)");
            if (applicationAdmin is not null || ledgerAdmin is not null)
                await ExecuteAsync(postgres.AdminConnectionString, $"DROP ROLE IF EXISTS {ApplicationRole}, {LedgerRole}");
        }
        finally
        {
            await postgres.DisposeAsync();
        }
    }

    private sealed class LedgerFaults(V01Fixture owner) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri?.AbsolutePath ?? "";
            var isPosting = request.Method == HttpMethod.Post && path == "/internal/v1/operations";
            if ((isPosting && Volatile.Read(ref owner.blockPosting) != 0) ||
                ((path.StartsWith("/internal/v1/operations", StringComparison.Ordinal) ||
                  path.EndsWith("/financial-state", StringComparison.Ordinal)) && Volatile.Read(ref owner.blockOperations) != 0))
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            var result = await base.SendAsync(request, ct);
            if (isPosting && result.IsSuccessStatusCode && Interlocked.Exchange(ref owner.dropNextResponse, 0) != 0)
            {
                result.Dispose();
                if (Interlocked.Exchange(ref owner.blockAfterDrop, 0) != 0)
                    Interlocked.Exchange(ref owner.blockOperations, 1);
                Interlocked.Exchange(ref owner.observedDroppedResponse, 1);
                throw new HttpRequestException("V01 injected lost response");
            }
            return result;
        }
    }
}
