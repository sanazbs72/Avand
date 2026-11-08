using Avand.V01.IntegrationTests.Fixtures;
using Npgsql;
using Xunit;

namespace Avand.V01.IntegrationTests;

public sealed class V02LedgerStorageTests(V02Fixture fixture) : IClassFixture<V02Fixture>
{
    [Fact]
    public Task UpgradePreservesV01FinancialHistory() => fixture.AssertLegacyUpgradeAsync();

    [Fact]
    public async Task RuntimeCannotWriteBalancesOrAppendArbitraryJournals()
    {
        foreach (var sql in new[]
        {
            """UPDATE "Accounts" SET "BalanceMinor" = "BalanceMinor" WHERE false""",
            """INSERT INTO "Journals" ("Id", "PostedAt", "Template") VALUES (gen_random_uuid(), now(), 'F04')""",
            """DELETE FROM "JournalLines" WHERE false"""
        })
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => fixture.WriteLedgerAsRuntimeAsync(sql));
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
        }
    }

    [Fact]
    public async Task PostedHistoryIsAppendOnlyEvenForTheOwner()
    {
        await fixture.FundAsync(1_000_000);
        foreach (var sql in new[]
        {
            """UPDATE "Journals" SET "PostedAt" = "PostedAt" """,
            """DELETE FROM "JournalLines" """,
            """UPDATE "Accounts" SET "Asset" = 'AU' WHERE "Bucket" = 'CustomerRialAvailable'"""
        })
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => fixture.WriteLedgerAsync(sql));
            Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        }
    }

    [Fact]
    public async Task BalancedLegsOutsideTheF04TemplateAreRejected()
    {
        await fixture.FundAsync(1_000_000);
        var before = await fixture.JournalCountAsync();
        var error = await Assert.ThrowsAsync<PostgresException>(() => fixture.WriteLedgerAsync("""
            DO $$
            DECLARE j uuid := gen_random_uuid();
            BEGIN
              INSERT INTO "Journals" ("Id", "PostedAt", "Template") VALUES (j, now(), 'F04');
              INSERT INTO "JournalLines" ("JournalId", "AccountId", "IsDebit", "AmountMinor")
              SELECT j, "Id", "Bucket" = 'CustomerRialAvailable', 100 FROM "Accounts"
              WHERE ("Bucket" = 'CustomerRialAvailable' AND "BalanceMinor" >= 1000000
                     AND "Owner" = (SELECT min("Owner") FROM "Accounts"
                                    WHERE "Bucket" = 'CustomerRialAvailable' AND "BalanceMinor" >= 1000000))
                 OR ("Owner" = 'platform' AND "Bucket" = 'TradingRialClearing');
              UPDATE "Accounts" a SET "BalanceMinor" = a."BalanceMinor" +
                  CASE WHEN l."IsDebit" = a."DebitNormal" THEN l."AmountMinor" ELSE -l."AmountMinor" END
              FROM "JournalLines" l WHERE l."JournalId" = j AND a."Id" = l."AccountId";
            END $$;
            """));
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Contains("Invalid Ledger journal template", error.MessageText);
        Assert.Equal(before, await fixture.JournalCountAsync());
    }

    [Fact]
    public async Task OwnerCannotCreateEmptyOrUnbalancedFinancialJournals()
    {
        await fixture.FundAsync(2_000_000);
        var before = await fixture.JournalCountAsync();
        var empty = await Assert.ThrowsAsync<PostgresException>(() => fixture.WriteLedgerAsync("""
            INSERT INTO "Journals" ("Id", "PostedAt", "Template") VALUES (gen_random_uuid(), now(), 'F04')
            """));
        Assert.Equal(PostgresErrorCodes.CheckViolation, empty.SqlState);

        var imbalance = await Assert.ThrowsAsync<PostgresException>(() => fixture.WriteLedgerAsync("""
            DO $$
            DECLARE
              j uuid := gen_random_uuid();
              wallet_owner text;
            BEGIN
              SELECT "Owner" INTO STRICT wallet_owner FROM "Accounts"
              WHERE "Bucket" = 'CustomerRialAvailable' AND "BalanceMinor" >= 2000000;
              INSERT INTO "Accounts" ("Id", "Owner", "Bucket", "BalanceMinor", "Asset", "DebitNormal")
              VALUES (gen_random_uuid(), wallet_owner, 'CustomerGoldAvailable', 0, 'AU', false);
              INSERT INTO "Journals" ("Id", "PostedAt", "Template") VALUES (j, now(), 'F04');
              INSERT INTO "JournalLines" ("JournalId", "AccountId", "IsDebit", "AmountMinor")
              SELECT j, "Id", "Bucket" IN ('CustomerRialAvailable', 'PlatformGoldPosition'),
                CASE "Bucket" WHEN 'CustomerRialAvailable' THEN 2000000 WHEN 'TradingRialClearing' THEN 1999999
                  WHEN 'CustomerGoldAvailable' THEN 101 ELSE 100 END
              FROM "Accounts"
              WHERE ("Owner" = wallet_owner AND "Bucket" IN ('CustomerRialAvailable', 'CustomerGoldAvailable'))
                 OR ("Owner" = 'platform' AND "Bucket" IN ('TradingRialClearing', 'PlatformGoldPosition'));
              UPDATE "Accounts" a SET "BalanceMinor" = a."BalanceMinor" +
                  CASE WHEN l."IsDebit" = a."DebitNormal" THEN l."AmountMinor" ELSE -l."AmountMinor" END
              FROM "JournalLines" l WHERE l."JournalId" = j AND a."Id" = l."AccountId";
            END $$;
            """));
        Assert.Equal(PostgresErrorCodes.CheckViolation, imbalance.SqlState);
        Assert.Contains("does not balance per asset", imbalance.MessageText);
        Assert.Equal(before, await fixture.JournalCountAsync());
    }
}
