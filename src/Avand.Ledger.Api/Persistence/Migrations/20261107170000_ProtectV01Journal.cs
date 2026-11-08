using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Avand.Ledger.Api.Persistence.Migrations;

[DbContext(typeof(LedgerDbContext))]
[Migration("20261107170000_ProtectV01Journal")]
public sealed class ProtectV01Journal : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE FUNCTION avand_reject_journal_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            RAISE EXCEPTION 'Ledger journal is append-only' USING ERRCODE = '23514';
        END;
        $$;
        CREATE TRIGGER avand_journal_immutable BEFORE UPDATE OR DELETE ON "Journals"
            FOR EACH ROW EXECUTE FUNCTION avand_reject_journal_mutation();
        CREATE TRIGGER avand_lines_immutable BEFORE UPDATE OR DELETE ON "JournalLines"
            FOR EACH ROW EXECUTE FUNCTION avand_reject_journal_mutation();

        CREATE FUNCTION avand_check_journal() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE journal_id uuid;
        BEGIN
            IF TG_TABLE_NAME = 'Journals' THEN journal_id := NEW."Id";
            ELSE journal_id := NEW."JournalId"; END IF;
            IF NOT EXISTS (
                SELECT 1 FROM "JournalLines" AS l JOIN "Accounts" AS a ON a."Id" = l."AccountId"
                WHERE l."JournalId" = journal_id
                HAVING count(*) = 2
                   AND count(*) FILTER (WHERE l."IsDebit" AND a."Bucket" = 'GatewayReceivable') = 1
                   AND count(*) FILTER (WHERE NOT l."IsDebit" AND a."Bucket" IN
                       ('CustomerRialAvailable', 'CustomerRialRestricted')) = 1
                   AND sum(CASE WHEN l."IsDebit" THEN l."AmountIrr" ELSE -l."AmountIrr" END) = 0
            ) THEN RAISE EXCEPTION 'Invalid V01 IRR journal' USING ERRCODE = '23514'; END IF;
            RETURN NULL;
        END;
        $$;
        CREATE CONSTRAINT TRIGGER avand_journal_balanced AFTER INSERT ON "Journals"
            DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION avand_check_journal();
        CREATE CONSTRAINT TRIGGER avand_new_lines_balanced AFTER INSERT ON "JournalLines"
            DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION avand_check_journal();

        CREATE FUNCTION avand_check_projection() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE account_id uuid;
        BEGIN
            IF TG_TABLE_NAME = 'Accounts' THEN account_id := NEW."Id";
            ELSE account_id := NEW."AccountId"; END IF;
            IF EXISTS (
                SELECT 1 FROM "Accounts" a WHERE a."Id" = account_id AND a."BalanceIrr" <>
                    (SELECT coalesce(sum(l."AmountIrr"), 0) FROM "JournalLines" l WHERE l."AccountId" = account_id)
            ) THEN RAISE EXCEPTION 'Ledger projection differs from journal' USING ERRCODE = '23514'; END IF;
            RETURN NULL;
        END;
        $$;
        CREATE CONSTRAINT TRIGGER avand_account_projection AFTER INSERT OR UPDATE ON "Accounts"
            DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION avand_check_projection();
        CREATE CONSTRAINT TRIGGER avand_line_projection AFTER INSERT ON "JournalLines"
            DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION avand_check_projection();
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TRIGGER avand_line_projection ON "JournalLines";
        DROP TRIGGER avand_account_projection ON "Accounts";
        DROP FUNCTION avand_check_projection();
        DROP TRIGGER avand_new_lines_balanced ON "JournalLines";
        DROP TRIGGER avand_journal_balanced ON "Journals";
        DROP FUNCTION avand_check_journal();
        DROP TRIGGER avand_lines_immutable ON "JournalLines";
        DROP TRIGGER avand_journal_immutable ON "Journals";
        DROP FUNCTION avand_reject_journal_mutation();
        """);
}
