namespace Avand.Ledger.Api.Persistence.Migrations;

/// <summary>
/// V03 posting boundary: F05 SellGold and fixture/owner-only OpeningCash templates, the single liquidity guard and
/// the authoritative free-liquidity calculation (chart of accounts section 7, spec C-03).
/// </summary>
internal static class V03JournalSql
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
                WHEN 'F05' THEN count(*) IN (4, 5)
                    AND count(*) FILTER (WHERE l."IsDebit" AND a."Bucket" = 'CustomerGoldAvailable') = 1
                    AND count(*) FILTER (WHERE NOT l."IsDebit" AND a."Bucket" = 'PlatformGoldPosition' AND a."Owner" = 'platform') = 1
                    AND count(*) FILTER (WHERE l."IsDebit" AND a."Bucket" = 'TradingRialClearing' AND a."Owner" = 'platform') = 1
                    AND count(*) FILTER (WHERE NOT l."IsDebit" AND a."Bucket" = 'CustomerRialAvailable') = 1
                    AND count(*) FILTER (WHERE NOT l."IsDebit" AND a."Bucket" = 'FeeRevenue' AND a."Owner" = 'platform') = count(*) - 4
                    AND max(a."Owner") FILTER (WHERE a."Bucket" = 'CustomerRialAvailable') =
                        max(a."Owner") FILTER (WHERE a."Bucket" = 'CustomerGoldAvailable')
                WHEN 'OpeningCustody' THEN count(*) = 2
                    AND count(*) FILTER (WHERE l."IsDebit" AND a."Bucket" = 'CustodianGold') = 1
                    AND count(*) FILTER (WHERE NOT l."IsDebit" AND a."Bucket" = 'PlatformGoldPosition' AND a."Owner" = 'platform') = 1
                WHEN 'OpeningCash' THEN count(*) = 2
                    AND count(*) FILTER (WHERE l."IsDebit" AND a."Bucket" = 'BankCash' AND a."Owner" = 'platform') = 1
                    AND count(*) FILTER (WHERE NOT l."IsDebit" AND a."Bucket" = 'PlatformCapital' AND a."Owner" = 'platform') = 1
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
                    WHERE d."JournalId" = journal_id))
                OR (template = 'F05' AND NOT EXISTS (SELECT 1 FROM public."SellDecisions" d
                    WHERE d."JournalId" = journal_id)) THEN
                RAISE EXCEPTION 'Ledger journal has no durable operation' USING ERRCODE = '23514';
            END IF;
            RETURN NULL;
        END;
        $$;

        -- Runtime posting boundary. OpeningCash stays owner/fixture-only: it is not accepted here.
        CREATE OR REPLACE FUNCTION public.avand_post_journal(j uuid, posted_at timestamptz, template text, ids uuid[],
            debits boolean[], amounts bigint[]) RETURNS void LANGUAGE plpgsql
        SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
        BEGIN
            IF template NOT IN ('F02', 'F04', 'F05') OR cardinality(ids) NOT BETWEEN 2 AND 5
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

        -- Shared liquidity guard. Every writer that can lower free liquidity takes it, after the backing guard and
        -- before eligibility and account rows.
        CREATE FUNCTION public.avand_lock_liquidity() RETURNS SETOF public."LiquidityControl" LANGUAGE sql
        SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
            SELECT c.* FROM public."LiquidityControl" c WHERE c."Id" = 1 FOR UPDATE
        $$;

        -- FreeLiquidity = BankCash - customer cash claims - other cash liabilities - H - R, in one statement.
        -- Buckets not yet created by any flow contribute zero. Receivables never add. NULL if the control row is missing.
        CREATE FUNCTION public.avand_free_liquidity() RETURNS bigint LANGUAGE sql STABLE
        SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
            SELECT (
                coalesce(sum(a."BalanceMinor"::numeric) FILTER (WHERE a."Bucket" = 'BankCash' AND a."Owner" = 'platform'), 0)
                - coalesce(sum(a."BalanceMinor"::numeric) FILTER (WHERE a."Bucket" IN (
                    'CustomerRialAvailable', 'CustomerRialReserved', 'CustomerRialRestricted',
                    'WithdrawalPayable', 'RefundPayable',
                    'CustodianPurchasePayable', 'GatewayRefundPayable', 'OperationsPayable', 'SuspenseCredit')), 0)
                - (SELECT c."CommitmentsIrr"::numeric + c."BufferIrr"::numeric FROM public."LiquidityControl" c WHERE c."Id" = 1)
            )::bigint
            FROM public."Accounts" a
        $$;

        REVOKE EXECUTE ON FUNCTION public.avand_lock_liquidity() FROM PUBLIC;
        REVOKE EXECUTE ON FUNCTION public.avand_free_liquidity() FROM PUBLIC;
        REVOKE INSERT, UPDATE, DELETE, TRUNCATE ON public."LiquidityControl", public."SellDecisions",
            public."SellBusinessClaims", public."SellQuoteClaims" FROM PUBLIC;

        -- Upgrade existing runtime roles: those already allowed to use the V02 posting boundary.
        DO $$
        DECLARE ledger_role text;
        BEGIN
            FOR ledger_role IN
                SELECT r.rolname FROM pg_roles r
                WHERE r.rolname <> current_user AND NOT r.rolsuper AND r.rolname NOT LIKE 'pg\_%'
                  AND has_function_privilege(r.oid,
                      'public.avand_post_journal(uuid, timestamptz, text, uuid[], boolean[], bigint[])', 'EXECUTE')
            LOOP
                EXECUTE format('GRANT EXECUTE ON FUNCTION public.avand_lock_liquidity(), public.avand_free_liquidity() TO %I', ledger_role);
                EXECUTE format('GRANT SELECT ON public."LiquidityControl", public."SellDecisions", public."SellBusinessClaims", public."SellQuoteClaims" TO %I', ledger_role);
                EXECUTE format('GRANT INSERT ON public."SellDecisions", public."SellBusinessClaims", public."SellQuoteClaims" TO %I', ledger_role);
            END LOOP;
        END $$;
        """;
}
