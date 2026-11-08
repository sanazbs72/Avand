using Avand.V01.IntegrationTests.Fixtures;
using Npgsql;
using Xunit;

namespace Avand.V01.IntegrationTests;

/// <summary>T004: V03 migration, liquidity storage and posting-boundary checks on real PostgreSQL.</summary>
public sealed class V03LedgerStorageTests(V03Fixture fixture) : IClassFixture<V03Fixture>
{
    [Fact]
    public Task UpgradePreservesV01AndV02HistoryAndGrantsRuntime() => fixture.AssertV03UpgradeAsync();

    [Fact]
    public async Task RuntimeCannotChangeLiquidityControlsOrWriteFinancialRows()
    {
        foreach (var sql in new[]
        {
            """UPDATE "LiquidityControl" SET "BufferIrr" = 0 WHERE false""",
            """INSERT INTO "Journals" ("Id", "PostedAt", "Template") VALUES (gen_random_uuid(), now(), 'F05')""",
            """UPDATE "Accounts" SET "BalanceMinor" = "BalanceMinor" WHERE "Bucket" = 'BankCash'""",
            """DELETE FROM "SellDecisions" WHERE false"""
        })
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => fixture.WriteLedgerAsRuntimeAsync(sql));
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
        }
    }

    [Fact]
    public async Task RuntimeCannotPostOpeningCash()
    {
        var before = await fixture.FreeLiquidityAsync();
        var error = await Assert.ThrowsAsync<PostgresException>(() => fixture.WriteLedgerAsRuntimeAsync("""
            SELECT public.avand_post_journal(gen_random_uuid(), now(), 'OpeningCash',
                ARRAY[(SELECT "Id" FROM "Accounts" WHERE "Bucket" = 'BankCash'),
                      (SELECT "Id" FROM "Accounts" WHERE "Bucket" = 'PlatformCapital')],
                ARRAY[true, false], ARRAY[1000::bigint, 1000::bigint])
            """));
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal(before, await fixture.FreeLiquidityAsync());
    }

    [Fact]
    public async Task FixtureCashRaisesFreeLiquidityAndReceivablesDoNot()
    {
        var start = await fixture.FreeLiquidityAsync();
        await fixture.FundCashAsync(10_000_000);
        Assert.Equal(start + 10_000_000, await fixture.FreeLiquidityAsync());

        // A V01 top-up raises the customer's claim against a gateway receivable, which is not cash.
        await fixture.FundAsync(1_000_000);
        Assert.Equal(start + 9_000_000, await fixture.FreeLiquidityAsync());

        await fixture.SetLiquidityControlAsync(commitmentsIrr: 2_000_000, bufferIrr: 500_000);
        Assert.Equal(start + 6_500_000, await fixture.FreeLiquidityAsync());
        await fixture.SetLiquidityControlAsync(0, 0);
    }

    [Fact]
    public async Task TopUpCreditsOnceEvenWhenFreeLiquidityIsNegative()
    {
        // T007 / Analyze A4: capture takes the liquidity guard but is never gated by liquidity.
        await fixture.SetLiquidityControlAsync(commitmentsIrr: 0, bufferIrr: 1_000_000_000_000);
        try
        {
            var liquidity = await fixture.FreeLiquidityAsync();
            Assert.True(liquidity < 0);
            var wallet = await fixture.AvailableAsync(fixture.CustomerId);

            await fixture.FundAsync(1_000_000);

            Assert.Equal(wallet + 1_000_000, await fixture.AvailableAsync(fixture.CustomerId));
            Assert.Equal(liquidity - 1_000_000, await fixture.FreeLiquidityAsync());
        }
        finally
        {
            await fixture.SetLiquidityControlAsync(0, 0);
        }
    }

    [Fact]
    public async Task TopUpCreditWaitsForTheLiquidityGuard()
    {
        var wallet = await fixture.AvailableAsync(fixture.CustomerId);
        var topUp = await fixture.StartAsync(amountIrr: 1_000_000);
        await fixture.AwaitStatusAsync(topUp, "AwaitingProvider");
        fixture.Gateway.SetResult(topUp, "Ready");

        Task notify;
        await using (await fixture.HoldLiquidityAsync())
        {
            notify = fixture.NotifyAsync(topUp);
            await fixture.AwaitBlockedLedgerSessionsAsync(1);
            Assert.Equal(wallet, await fixture.AvailableAsync(fixture.CustomerId));
        }

        await notify;
        await fixture.AwaitStatusAsync(topUp, "Succeeded");
        Assert.Equal(wallet + 1_000_000, await fixture.AvailableAsync(fixture.CustomerId));
    }

    [Fact]
    public async Task F05WithWrongLegsIsRejected()
    {
        await fixture.GiveGoldAsync(fixture.CustomerId, 500);
        var before = await fixture.JournalCountAsync();
        // Balanced per asset but missing the TradingRialClearing debit and customer IRR credit.
        var error = await Assert.ThrowsAsync<PostgresException>(() => fixture.WriteLedgerAsync("""
            DO $$
            DECLARE j uuid := gen_random_uuid();
            BEGIN
              INSERT INTO "Journals" ("Id", "PostedAt", "Template") VALUES (j, now(), 'F05');
              INSERT INTO "JournalLines" ("JournalId", "AccountId", "IsDebit", "AmountMinor")
              SELECT j, "Id", "Bucket" = 'CustomerGoldAvailable', 100 FROM "Accounts"
              WHERE ("Owner" = $1 AND "Bucket" = 'CustomerGoldAvailable')
                 OR ("Owner" = 'platform' AND "Bucket" = 'PlatformGoldPosition');
              UPDATE "Accounts" a SET "BalanceMinor" = a."BalanceMinor" +
                  CASE WHEN l."IsDebit" = a."DebitNormal" THEN l."AmountMinor" ELSE -l."AmountMinor" END
              FROM "JournalLines" l WHERE l."JournalId" = j AND a."Id" = l."AccountId";
            END $$;
            """.Replace("$1", $"'{fixture.CustomerId:D}'")));
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Contains("Invalid Ledger journal template", error.MessageText);
        Assert.Equal(before, await fixture.JournalCountAsync());
    }

    [Fact]
    public async Task WellFormedF05WithoutSellDecisionIsRejected()
    {
        await fixture.GiveGoldAsync(fixture.OtherId, 500, fixture.Other);
        var before = await fixture.JournalCountAsync();
        var error = await Assert.ThrowsAsync<PostgresException>(() => fixture.WriteLedgerAsync("""
            DO $$
            DECLARE j uuid := gen_random_uuid();
            BEGIN
              INSERT INTO "Journals" ("Id", "PostedAt", "Template") VALUES (j, now(), 'F05');
              INSERT INTO "JournalLines" ("JournalId", "AccountId", "IsDebit", "AmountMinor")
              SELECT j, "Id", "Bucket" IN ('CustomerGoldAvailable', 'TradingRialClearing'),
                CASE "Bucket" WHEN 'CustomerGoldAvailable' THEN 500 WHEN 'PlatformGoldPosition' THEN 500
                  WHEN 'TradingRialClearing' THEN 4900000 WHEN 'CustomerRialAvailable' THEN 4875500 ELSE 24500 END
              FROM "Accounts"
              WHERE ("Owner" = 'OWNER' AND "Bucket" IN ('CustomerGoldAvailable', 'CustomerRialAvailable'))
                 OR ("Owner" = 'platform' AND "Bucket" IN ('PlatformGoldPosition', 'TradingRialClearing', 'FeeRevenue'));
              UPDATE "Accounts" a SET "BalanceMinor" = a."BalanceMinor" +
                  CASE WHEN l."IsDebit" = a."DebitNormal" THEN l."AmountMinor" ELSE -l."AmountMinor" END
              FROM "JournalLines" l WHERE l."JournalId" = j AND a."Id" = l."AccountId";
            END $$;
            """.Replace("OWNER", fixture.OtherId.ToString("D"))));
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Contains("no durable operation", error.MessageText);
        Assert.Equal(before, await fixture.JournalCountAsync());
    }
}
