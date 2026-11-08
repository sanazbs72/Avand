namespace Avand.Ledger.Api.Persistence.Migrations;

internal static class V02JournalSql
{
    internal const string Upgrade = """
        CREATE OR REPLACE FUNCTION public.avand_check_journal() RETURNS trigger LANGUAGE plpgsql
        SET search_path = pg_catalog, public AS $$
        DECLARE
            journal_id uuid;
            template text;
            valid_shape boolean;
        BEGIN
            IF TG_TABLE_NAME = 'Journals' THEN journal_id := NEW."Id";
            ELSE journal_id := NEW."JournalId"; END IF;
            SELECT j."Template" INTO template FROM public."Journals" j WHERE j."Id" = journal_id;

            IF EXISTS (
                SELECT 1 FROM public."JournalLines" l JOIN public."Accounts" a ON a."Id" = l."AccountId"
                WHERE l."JournalId" = journal_id
                GROUP BY a."Asset"
                HAVING sum(CASE WHEN l."IsDebit" THEN l."AmountMinor"::numeric ELSE -l."AmountMinor"::numeric END) <> 0
            ) THEN RAISE EXCEPTION 'Ledger journal does not balance per asset' USING ERRCODE = '23514'; END IF;

            SELECT CASE template
                WHEN 'F02' THEN count(*) = 2
                    AND count(*) FILTER (WHERE l."IsDebit" AND a."Bucket" = 'GatewayReceivable') = 1
                    AND count(*) FILTER (WHERE NOT l."IsDebit" AND a."Bucket" IN
                        ('CustomerRialAvailable', 'CustomerRialRestricted')) = 1
                WHEN 'F04' THEN count(*) IN (4, 5)
                    AND count(*) FILTER (WHERE l."IsDebit" AND a."Bucket" = 'CustomerRialAvailable') = 1
                    AND count(*) FILTER (WHERE l."IsDebit" AND a."Bucket" = 'PlatformGoldPosition' AND a."Owner" = 'platform') = 1
                    AND count(*) FILTER (WHERE NOT l."IsDebit" AND a."Bucket" = 'TradingRialClearing' AND a."Owner" = 'platform') = 1
                    AND count(*) FILTER (WHERE NOT l."IsDebit" AND a."Bucket" = 'CustomerGoldAvailable') = 1
                    AND count(*) FILTER (WHERE NOT l."IsDebit" AND a."Bucket" = 'FeeRevenue' AND a."Owner" = 'platform') = count(*) - 4
                    AND max(a."Owner") FILTER (WHERE a."Bucket" = 'CustomerRialAvailable') =
                        max(a."Owner") FILTER (WHERE a."Bucket" = 'CustomerGoldAvailable')
                WHEN 'OpeningCustody' THEN count(*) = 2
                    AND count(*) FILTER (WHERE l."IsDebit" AND a."Bucket" = 'CustodianGold') = 1
                    AND count(*) FILTER (WHERE NOT l."IsDebit" AND a."Bucket" = 'PlatformGoldPosition' AND a."Owner" = 'platform') = 1
                ELSE false END
            INTO valid_shape
            FROM public."JournalLines" l JOIN public."Accounts" a ON a."Id" = l."AccountId"
            WHERE l."JournalId" = journal_id;

            IF valid_shape IS DISTINCT FROM true THEN
                RAISE EXCEPTION 'Invalid Ledger journal template' USING ERRCODE = '23514';
            END IF;
            IF (template = 'F02' AND NOT EXISTS (SELECT 1 FROM public."OperationDecisions" d
                    WHERE d."JournalId" = journal_id))
                OR (template = 'F04' AND NOT EXISTS (SELECT 1 FROM public."BuyDecisions" d
                    WHERE d."JournalId" = journal_id)) THEN
                RAISE EXCEPTION 'Ledger journal has no durable operation' USING ERRCODE = '23514';
            END IF;
            RETURN NULL;
        END;
        $$;

        CREATE OR REPLACE FUNCTION public.avand_check_projection() RETURNS trigger LANGUAGE plpgsql
        SET search_path = pg_catalog, public AS $$
        DECLARE account_id uuid;
        BEGIN
            IF TG_TABLE_NAME = 'Accounts' THEN account_id := NEW."Id";
            ELSE account_id := NEW."AccountId"; END IF;
            IF EXISTS (
                SELECT 1 FROM public."Accounts" a WHERE a."Id" = account_id AND a."BalanceMinor" <>
                    (SELECT coalesce(sum(CASE WHEN l."IsDebit" = a."DebitNormal" THEN l."AmountMinor"::numeric
                        ELSE -l."AmountMinor"::numeric END), 0)
                     FROM public."JournalLines" l WHERE l."AccountId" = account_id)
            ) THEN RAISE EXCEPTION 'Ledger projection differs from journal' USING ERRCODE = '23514'; END IF;
            RETURN NULL;
        END;
        $$;

        CREATE FUNCTION public.avand_lock_accounts(ids uuid[]) RETURNS SETOF public."Accounts" LANGUAGE sql
        SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
            SELECT a.* FROM public."Accounts" a WHERE a."Id" = ANY(ids) ORDER BY a."Id" FOR UPDATE
        $$;

        CREATE FUNCTION public.avand_post_journal(j uuid, posted_at timestamptz, template text, ids uuid[],
            debits boolean[], amounts bigint[]) RETURNS void LANGUAGE plpgsql
        SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
        BEGIN
            IF template NOT IN ('F02', 'F04') OR cardinality(ids) NOT BETWEEN 2 AND 5
                OR cardinality(ids) IS DISTINCT FROM cardinality(debits)
                OR cardinality(ids) IS DISTINCT FROM cardinality(amounts) THEN
                RAISE EXCEPTION 'Invalid Ledger posting request' USING ERRCODE = '23514';
            END IF;

            PERFORM a."Id" FROM public."Accounts" a WHERE a."Id" = ANY(ids) ORDER BY a."Id" FOR UPDATE;
            INSERT INTO public."Journals" ("Id", "PostedAt", "Template") VALUES (j, posted_at, template);
            INSERT INTO public."JournalLines" ("JournalId", "AccountId", "IsDebit", "AmountMinor")
                SELECT j, leg.id, leg.debit, leg.amount FROM unnest(ids, debits, amounts) AS leg(id, debit, amount);
            UPDATE public."Accounts" a
            SET "BalanceMinor" = a."BalanceMinor" + CASE WHEN leg.debit = a."DebitNormal" THEN leg.amount ELSE -leg.amount END
            FROM unnest(ids, debits, amounts) AS leg(id, debit, amount)
            WHERE a."Id" = leg.id;
        END;
        $$;

        CREATE FUNCTION public.avand_lock_backing() RETURNS SETOF public."GoldBacking" LANGUAGE sql
        SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
            SELECT g.* FROM public."GoldBacking" g WHERE g."Id" = 1 FOR UPDATE
        $$;

        CREATE FUNCTION public.avand_open_gold_account(customer uuid) RETURNS uuid LANGUAGE sql
        SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
            INSERT INTO public."Accounts" AS existing ("Id", "Owner", "Bucket", "BalanceMinor", "Asset", "DebitNormal")
            VALUES (gen_random_uuid(), customer::text, 'CustomerGoldAvailable', 0, 'AU', false)
            ON CONFLICT ("Owner", "Bucket") DO UPDATE SET "BalanceMinor" = existing."BalanceMinor"
            RETURNING "Id"
        $$;

        REVOKE EXECUTE ON FUNCTION public.avand_lock_backing() FROM PUBLIC;
        REVOKE EXECUTE ON FUNCTION public.avand_lock_accounts(uuid[]) FROM PUBLIC;
        REVOKE EXECUTE ON FUNCTION public.avand_post_journal(uuid, timestamptz, text, uuid[], boolean[], bigint[]) FROM PUBLIC;
        REVOKE EXECUTE ON FUNCTION public.avand_open_gold_account(uuid) FROM PUBLIC;

        CREATE FUNCTION public.avand_account_identity_immutable() RETURNS trigger LANGUAGE plpgsql
        SET search_path = pg_catalog, pg_temp AS $$
        BEGIN
            IF (NEW."Id", NEW."Owner", NEW."Bucket", NEW."Asset", NEW."DebitNormal") IS DISTINCT FROM
                (OLD."Id", OLD."Owner", OLD."Bucket", OLD."Asset", OLD."DebitNormal") THEN
                RAISE EXCEPTION 'Ledger account identity is immutable' USING ERRCODE = '23514';
            END IF;
            RETURN NEW;
        END;
        $$;
        CREATE TRIGGER avand_account_identity BEFORE UPDATE ON public."Accounts"
            FOR EACH ROW EXECUTE FUNCTION public.avand_account_identity_immutable();

        REVOKE INSERT, UPDATE, DELETE, TRUNCATE ON public."Accounts", public."Journals", public."JournalLines" FROM PUBLIC;
        REVOKE UPDATE ("BalanceMinor") ON public."Accounts" FROM PUBLIC;
        DO $$
        DECLARE ledger_role text;
        BEGIN
            FOR ledger_role IN
                SELECT grantee FROM information_schema.table_privileges
                WHERE table_schema = 'public' AND table_name = 'Journals' AND privilege_type = 'INSERT'
                    AND grantee NOT IN ('PUBLIC', current_user)
                INTERSECT
                SELECT grantee FROM information_schema.table_privileges
                WHERE table_schema = 'public' AND table_name = 'OperationDecisions' AND privilege_type = 'INSERT'
            LOOP
                EXECUTE format('REVOKE INSERT, UPDATE, DELETE, TRUNCATE ON public."Accounts", public."Journals", public."JournalLines" FROM %I', ledger_role);
                EXECUTE format('REVOKE UPDATE ("BalanceMinor") ON public."Accounts" FROM %I', ledger_role);
                EXECUTE format('GRANT EXECUTE ON FUNCTION public.avand_lock_accounts(uuid[]), public.avand_lock_backing(),
                    public.avand_post_journal(uuid, timestamptz, text, uuid[], boolean[], bigint[]),
                    public.avand_open_gold_account(uuid) TO %I', ledger_role);
                EXECUTE format('GRANT SELECT ON public."GoldBacking", public."BuyDecisions", public."BuyBusinessClaims", public."BuyQuoteClaims" TO %I', ledger_role);
                EXECUTE format('GRANT INSERT ON public."BuyDecisions", public."BuyBusinessClaims", public."BuyQuoteClaims" TO %I', ledger_role);
            END LOOP;
        END $$;
        """;
}
