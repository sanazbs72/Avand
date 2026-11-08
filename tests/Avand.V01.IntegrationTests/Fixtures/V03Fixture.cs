using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Avand.Ledger.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Avand.V01.IntegrationTests.Fixtures;

/// <summary>
/// V03 sell fixture on top of the V02 hosts, clocks and paired price. Customers receive gold only through a real
/// V01 top-up followed by a real V02 buy. Bank cash comes only from the fixture-only OpeningCash entry, posted
/// with owner authority under the liquidity guard (T006).
/// </summary>
public sealed class V03Fixture : V02Fixture
{
    /// <summary>B07 sell-side price in IRR per gram.</summary>
    internal const long Bid = 9_800_000;
    internal const long Ask = 10_000_000;
    private const int BuyFeePpm = 10_000;
    private const string V02Migration = "20261108020535_V02AssetJournal";
    private IConfiguration? applicationConfig;

    protected override void ConfigureApplication(WebApplicationBuilder builder)
    {
        base.ConfigureApplication(builder);
        applicationConfig = builder.Configuration;
    }

    /// <summary>Configured Application sell fee (null restores the 5,000 ppm default).</summary>
    internal void SetSellRate(int? rate) =>
        applicationConfig!["V03:Policy:SellFeePpm"] = rate?.ToString(System.Globalization.CultureInfo.InvariantCulture);

    internal async Task<HttpResponseMessage> RequestSellQuoteAsync(object body, HttpClient? customer = null) =>
        await (customer ?? Customer).PostAsJsonAsync("/api/v1/gold/sell-quotes", body);

    internal async Task<JsonElement> SellQuoteAsync(long? quantityMg = null, long? requestedGrossIrr = null,
        HttpClient? customer = null)
    {
        using var response = await RequestSellQuoteAsync(new { quantityMg, requestedGrossIrr }, customer);
        Xunit.Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    internal async Task<HttpResponseMessage> ConfirmSellAsync(Guid quoteId, string key, HttpClient? customer = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/gold/sell-orders")
        {
            Content = JsonContent.Create(new { quoteId })
        };
        request.Headers.Add("Idempotency-Key", key);
        return await (customer ?? Customer).SendAsync(request);
    }

    internal async Task<Guid> ConfirmedSellAsync(Guid quoteId, string? key = null, HttpClient? customer = null)
    {
        using var reply = await ConfirmSellAsync(quoteId, key ?? Guid.NewGuid().ToString("N"), customer);
        Xunit.Assert.Equal(HttpStatusCode.Accepted, reply.StatusCode);
        return (await reply.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("orderId").GetGuid();
    }

    internal async Task<JsonElement> AwaitSellAsync(Guid orderId, string status, HttpClient? customer = null)
    {
        JsonElement order = default;
        await EventuallyAsync(async () =>
        {
            order = await (customer ?? Customer).GetFromJsonAsync<JsonElement>($"/api/v1/gold/sell-orders/{orderId}");
            return order.GetProperty("status").GetString() == status;
        });
        return order;
    }

    /// <summary>Quotes by weight, confirms and waits for a terminal status through both real hosts.</summary>
    internal async Task<JsonElement> SellAsync(long quantityMg, string expected, HttpClient? customer = null)
    {
        var quote = await SellQuoteAsync(quantityMg: quantityMg, customer: customer);
        var order = await ConfirmedSellAsync(quote.GetProperty("quoteId").GetGuid(), customer: customer);
        return await AwaitSellAsync(order, expected, customer);
    }

    /// <summary>Sets free liquidity to exactly <paramref name="exactIrr"/>: funds bank cash if it is short, then
    /// absorbs any excess with the operational buffer R. Commitments H are reset to zero.</summary>
    internal async Task SetFreeLiquidityAsync(long exactIrr)
    {
        await SetLiquidityControlAsync(0, 0);
        var current = await FreeLiquidityAsync();
        if (current < exactIrr)
            await FundCashAsync(exactIrr - current);
        var excess = await FreeLiquidityAsync() - exactIrr;
        await SetLiquidityControlAsync(0, excess);
        Xunit.Assert.Equal(exactIrr, await FreeLiquidityAsync());
    }

    internal async Task<Guid?> SellJournalAsync(Guid orderId)
    {
        await using var db = new NpgsqlConnection(LedgerAdmin);
        await db.OpenAsync();
        await using var command = new NpgsqlCommand("""SELECT "JournalId" FROM "SellDecisions" WHERE "OrderId" = $1""", db);
        command.Parameters.Add(new NpgsqlParameter { Value = orderId });
        var value = await command.ExecuteScalarAsync();
        return value is Guid id ? id : null;
    }

    /// <summary>Lines per asset and their signed sum (debit +, credit −) for one journal.</summary>
    internal Task<long> UnbalancedAssetCountAsync(Guid journalId) => ReadLedgerAsync("""
        SELECT count(*) FROM (
          SELECT a."Asset" FROM "JournalLines" l JOIN "Accounts" a ON a."Id" = l."AccountId"
          WHERE l."JournalId" = $1 GROUP BY a."Asset"
          HAVING sum(CASE WHEN l."IsDebit" THEN l."AmountMinor" ELSE -l."AmountMinor" END) <> 0) unbalanced
        """, journalId);

    internal Task<long> SellCountAsync(string where, params object[] values) =>
        ReadLedgerAsync($"""SELECT count(*) FROM "SellDecisions" WHERE {where}""", values);

    /// <summary>Observes the paired price used by the V03 examples: buy 10,000,000 and sell 9,800,000 IRR/g.</summary>
    internal void ObserveSellPrice(long bid = Bid) => ObservePrice(Ask, bid);

    /// <summary>
    /// Gives <paramref name="customerId"/> exactly <paramref name="goldMg"/> of available gold by funding the
    /// wallet through V01 and completing a V02 buy at the 10,000,000 IRR/g ask. Requires at least 100 mg so the
    /// buy principal meets the 1,000,000 IRR minimum. Leaves the wallet unchanged and restores the V03 price.
    /// </summary>
    internal async Task GiveGoldAsync(Guid customerId, long goldMg, HttpClient? customer = null)
    {
        if (goldMg < 100)
            throw new ArgumentOutOfRangeException(nameof(goldMg), "A V02 buy needs at least 100 mg at the fixture ask.");

        var principal = checked(goldMg * Ask / 1000);
        var fee = (principal * BuyFeePpm + 999_999) / 1_000_000;
        var goldBefore = await GoldMgAsync(customerId);
        var walletBefore = await AvailableAsync(customerId);

        await FundAsync(principal + fee, customer);
        ObserveSellPrice();
        var order = await BuyAsync(principal, "Completed", customer);

        Xunit.Assert.Equal(goldMg, order.GetProperty("quantityMg").GetInt64());
        Xunit.Assert.Equal(goldBefore + goldMg, await GoldMgAsync(customerId));
        Xunit.Assert.Equal(walletBefore, await AvailableAsync(customerId));
    }

    /// <summary>Committed available gold for one customer, read from the Ledger store.</summary>
    internal Task<long> GoldMgAsync(Guid customerId) => ReadLedgerAsync("""
        SELECT coalesce(sum("BalanceMinor"), 0)::bigint FROM "Accounts"
        WHERE "Owner" = $1 AND "Bucket" = 'CustomerGoldAvailable'
        """, customerId.ToString("D"));

    /// <summary>Fixture-only confirmed owner funding: debit BankCash, credit PlatformCapital, under the liquidity guard.</summary>
    internal Task FundCashAsync(long amountIrr) => WriteLedgerAsync($$"""
        DO $$
        DECLARE j uuid := gen_random_uuid();
        BEGIN
          PERFORM 1 FROM "LiquidityControl" WHERE "Id" = 1 FOR UPDATE;
          INSERT INTO "Journals" ("Id", "PostedAt", "Template") VALUES (j, now(), 'OpeningCash');
          INSERT INTO "JournalLines" ("JournalId", "AccountId", "IsDebit", "AmountMinor")
          SELECT j, "Id", "Bucket" = 'BankCash', {{amountIrr}} FROM "Accounts"
          WHERE "Owner" = 'platform' AND "Bucket" IN ('BankCash', 'PlatformCapital');
          UPDATE "Accounts" SET "BalanceMinor" = "BalanceMinor" + {{amountIrr}}
          WHERE "Owner" = 'platform' AND "Bucket" IN ('BankCash', 'PlatformCapital');
        END $$;
        """);

    internal Task<long> FreeLiquidityAsync() => ReadLedgerAsync("SELECT public.avand_free_liquidity()");

    /// <summary>Sets active unbooked cash commitments (H) and the operational buffer (R).</summary>
    internal Task SetLiquidityControlAsync(long commitmentsIrr, long bufferIrr) => WriteLedgerAsync("""
        UPDATE "LiquidityControl" SET "CommitmentsIrr" = $1, "BufferIrr" = $2, "Version" = "Version" + 1 WHERE "Id" = 1
        """, commitmentsIrr, bufferIrr);

    /// <summary>Builds a typed SellGold envelope (contract v03) from explicit snapshot values.</summary>
    internal static object SellEnvelope(Guid customer, Guid order, Guid operation, Guid quote, DateTimeOffset issuedAt,
        long quantityMg, long grossIrr, long feeIrr, long netIrr, long bid = Bid, int feeRatePpm = 5000,
        string side = "Sell", string policyVersion = "sell-v1") => new
    {
        schemaVersion = 1, operationId = operation, operationType = "SellGold",
        businessReference = new { kind = "GoldSellOrder", id = order, action = "Execute" },
        customerId = customer, actor = new { kind = "Customer", id = customer }, correlationId = order,
        payload = new
        {
            quoteSnapshot = new
            {
                side, quoteId = quote, issuedAt, expiresAt = issuedAt.AddSeconds(30), bidIrrPerGram = bid,
                quantityMg, grossIrr, feeRatePpm, feeIrr, netCreditIrr = netIrr, policyVersion
            }
        }
    };

    internal Task<long> PostedSellCountAsync(Guid orderId) =>
        SellCountAsync("\"OrderId\" = $1 AND \"JournalId\" IS NOT NULL", orderId);

    internal async Task<string> FrozenSellRequestAsync(Guid orderId)
    {
        await using var db = new NpgsqlConnection(ApplicationAdmin);
        await db.OpenAsync();
        await using var command = new NpgsqlCommand("""SELECT "Request" FROM "SellOrders" WHERE "Id" = $1""", db);
        command.Parameters.Add(new NpgsqlParameter { Value = orderId });
        return (string)(await command.ExecuteScalarAsync())!;
    }

    /// <summary>Fails the Ledger SellGold transaction at commit, after the journal and decision were written.</summary>
    internal Task ArmLedgerSellFailureAsync() => WriteLedgerAsync("""
        CREATE FUNCTION v03_fail_sell_commit() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN RAISE EXCEPTION 'V03 Ledger precommit fault'; END; $$;
        CREATE CONSTRAINT TRIGGER v03_fail_sell_commit AFTER INSERT ON "SellDecisions"
        DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION v03_fail_sell_commit();
        """);

    internal Task DisarmLedgerSellFailureAsync() => WriteLedgerAsync("""
        DROP TRIGGER IF EXISTS v03_fail_sell_commit ON "SellDecisions";
        DROP FUNCTION IF EXISTS v03_fail_sell_commit();
        """);

    /// <summary>Fails every Application sell-order status change (apply and unknown marking) after the Ledger commit.</summary>
    internal Task ArmApplicationSellApplyFailureAsync() => ExecuteAsync(ApplicationAdmin, """
        CREATE FUNCTION v03_fail_apply() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN RAISE EXCEPTION 'V03 Application apply fault'; END; $$;
        CREATE TRIGGER v03_fail_apply BEFORE UPDATE ON "SellOrders"
        FOR EACH ROW EXECUTE FUNCTION v03_fail_apply();
        """);

    internal Task DisarmApplicationSellApplyFailureAsync() => ExecuteAsync(ApplicationAdmin, """
        DROP TRIGGER IF EXISTS v03_fail_apply ON "SellOrders";
        DROP FUNCTION IF EXISTS v03_fail_apply();
        """);

    /// <summary>Holds the shared liquidity guard from an independent session until disposed.</summary>
    internal async Task<IAsyncDisposable> HoldLiquidityAsync()
    {
        var connection = new NpgsqlConnection(LedgerAdmin);
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync();
        await using (var command = new NpgsqlCommand("""SELECT 1 FROM "LiquidityControl" WHERE "Id" = 1 FOR UPDATE""",
            connection, transaction))
            await command.ExecuteScalarAsync();
        return new HeldGuard(connection, transaction);
    }

    /// <summary>Upgrades a populated V02 store (one V01 capture, one V02 buy) to the latest migration and checks
    /// history, new accounts, the liquidity control and the upgraded runtime grants.</summary>
    internal async Task AssertV03UpgradeAsync()
    {
        var database = "avand_v03_upgrade_" + Guid.NewGuid().ToString("N");
        var role = database + "_role";
        await ExecuteAsync(LedgerAdmin, $"CREATE DATABASE {database}");
        var connection = new NpgsqlConnectionStringBuilder(LedgerAdmin) { Database = database }.ConnectionString;
        try
        {
            await LedgerHost.MigrateAsync(connection, targetMigration: V02Migration);
            await ExecuteAsync(connection, $"""
                CREATE ROLE {role} NOLOGIN;
                GRANT EXECUTE ON FUNCTION public.avand_post_journal(uuid, timestamptz, text, uuid[], boolean[], bigint[]) TO {role};
                """);
            var customer = Guid.NewGuid();
            await ExecuteAsync(connection, $$"""
                DO $$
                DECLARE
                  cust text := '{{customer:D}}';
                  f02 uuid := gen_random_uuid(); f04 uuid := gen_random_uuid();
                  recv uuid := gen_random_uuid(); rial uuid := gen_random_uuid(); gold uuid := gen_random_uuid();
                  clearing uuid := '26020000-0000-0000-0000-000000000001';
                  fee uuid := '26020000-0000-0000-0000-000000000002';
                  position uuid := '26020000-0000-0000-0000-000000000003';
                BEGIN
                  INSERT INTO "Accounts" ("Id", "Owner", "Bucket", "BalanceMinor", "Asset", "DebitNormal") VALUES
                    (recv, 'mock', 'GatewayReceivable', 10100001, 'IRR', true),
                    (rial, cust, 'CustomerRialAvailable', 1, 'IRR', false),
                    (gold, cust, 'CustomerGoldAvailable', 1000, 'AU', false);
                  UPDATE "Accounts" SET "BalanceMinor" = CASE "Id" WHEN clearing THEN 10000000
                    WHEN fee THEN 100000 ELSE -1000 END WHERE "Id" IN (clearing, fee, position);
                  INSERT INTO "Journals" ("Id", "PostedAt", "Template") VALUES (f02, now(), 'F02'), (f04, now(), 'F04');
                  INSERT INTO "JournalLines" ("JournalId", "AccountId", "IsDebit", "AmountMinor") VALUES
                    (f02, recv, true, 10100001), (f02, rial, false, 10100001),
                    (f04, rial, true, 10100000), (f04, clearing, false, 10000000), (f04, fee, false, 100000),
                    (f04, position, true, 1000), (f04, gold, false, 1000);
                  INSERT INTO "OperationDecisions" ("Producer", "OperationId", "BusinessReference", "Fingerprint",
                    "CustomerId", "AmountIrr", "JournalId", "CreditedBucket", "DecidedAt")
                  VALUES ('Application.Payments', gen_random_uuid(), 'legacy', 'v1:legacy', cust::uuid, 10100001,
                    f02, 'CustomerRialAvailable', now());
                  INSERT INTO "BuyDecisions" ("OperationId", "OrderId", "QuoteId", "CustomerId", "Fingerprint",
                    "JournalId", "IncrementalUncoveredGoldMg", "DecidedAt")
                  VALUES (gen_random_uuid(), gen_random_uuid(), gen_random_uuid(), cust::uuid, 'v1:legacy', f04, 0, now());
                END $$;
                """);

            await LedgerHost.MigrateAsync(connection);

            Xunit.Assert.Equal(1, await ScalarAsync(connection, """
                SELECT CASE WHEN
                  (SELECT count(*) FROM "Journals" WHERE "Template" IN ('F02', 'F04')) = 2
                  AND (SELECT count(*) FROM "JournalLines") = 7
                  AND (SELECT "BalanceMinor" FROM "Accounts" WHERE "Owner" = $1 AND "Bucket" = 'CustomerRialAvailable') = 1
                  AND (SELECT "BalanceMinor" FROM "Accounts" WHERE "Owner" = $1 AND "Bucket" = 'CustomerGoldAvailable') = 1000
                  AND (SELECT count(*) FROM "Accounts" WHERE "Owner" = 'platform' AND "BalanceMinor" = 0
                       AND (("Bucket" = 'BankCash' AND "DebitNormal") OR ("Bucket" = 'PlatformCapital' AND NOT "DebitNormal"))) = 2
                  AND (SELECT count(*) FROM "LiquidityControl" WHERE "Id" = 1 AND "CommitmentsIrr" = 0 AND "BufferIrr" = 0) = 1
                THEN 1::bigint ELSE 0::bigint END
                """, customer.ToString("D")));

            // The V01 receivable is not cash, so the 1 IRR customer claim leaves free liquidity at -1.
            Xunit.Assert.Equal(-1, await ScalarAsync(connection, "SELECT public.avand_free_liquidity()"));

            Xunit.Assert.Equal(1, await ScalarAsync(connection, """
                SELECT CASE WHEN has_function_privilege($1, 'public.avand_lock_liquidity()', 'EXECUTE')
                  AND has_function_privilege($1, 'public.avand_free_liquidity()', 'EXECUTE')
                  AND has_table_privilege($1, 'public."SellDecisions"', 'INSERT')
                  AND has_table_privilege($1, 'public."SellQuoteClaims"', 'INSERT')
                  AND NOT has_table_privilege($1, 'public."LiquidityControl"', 'UPDATE')
                  AND NOT has_table_privilege($1, 'public."SellDecisions"', 'UPDATE')
                THEN 1::bigint ELSE 0::bigint END
                """, role));
        }
        finally
        {
            await ExecuteAsync(LedgerAdmin, $"DROP DATABASE {database} WITH (FORCE)");
            await ExecuteAsync(LedgerAdmin, $"DROP ROLE IF EXISTS {role}");
        }
    }
}
