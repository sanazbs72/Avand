# Quickstart — V03 validation

## Prerequisites

Same as [V02](../002-buy-gold-wallet/quickstart.md): .NET 10 SDK and a Docker-compatible engine for PostgreSQL 17.6, or `AVAND_V01_TEST_POSTGRES` for a disposable instance. Use synthetic customers, prices, custody and cash only.

## Commands after implementation

```powershell
dotnet build Avand.slnx --ignore-failed-sources
dotnet test tests/Avand.V01.IntegrationTests/Avand.V01.IntegrationTests.csproj --no-build --filter "FullyQualifiedName~V03"
dotnet test tests/Avand.V01.IntegrationTests/Avand.V01.IntegrationTests.csproj --no-build --filter "FullyQualifiedName!~V03"
```

Confirm the discovered test counts. The second command reruns V01 and V02 on the evolved schema. A build or an empty selection is not runtime evidence.

## Customer journey

1. Start both hosts with isolated stores and roles, controlled clocks and a paired price with bid 9,800,000 IRR/g. Fund `BankCash` through the fixture-only `OpeningCash` entry. Size it to cover all customer IRR claims created by V01 top-ups plus the free liquidity each test needs.
2. Give a seeded eligible customer gold with a real V01 top-up followed by a real V02 buy.
3. Request a sell quote for 500 mg (and separately for 4,900,001 IRR). Check gross 4,900,000, fee 24,500 and net 4,875,500 against an independent oracle. Confirm with a fresh key.
4. Read the order and balances. Inspect the F05 journal (both assets balance) and the stored result. Then confirm a V02 buy whose total debit is covered only by the net.

## Acceptance matrix

| Spec cases | Exercise and observe |
| --- | --- |
| AC-01, AC-01b | Both input modes, buy price ≠ sell price, fee rounding up (1,000 mg at 1,000,001 → fee 5,001, net 995,000), gross rounding down (101 mg at 9,900,999 → 1,000,000), leftover gold stays with the customer, configured rate change. |
| AC-02, AC-03 | Exact gold −q and IRR +N deltas, separate fee line, balanced F05. The net is in `CustomerRialAvailable` and immediately funds a V02 buy. Withdrawal reservation is deferred to V04. |
| AC-04 | Gross exactly at each limit passes. One rial under or over fails. A rial input of 1,000,000 that rounds to 999,999 fails. 500,000 mg vs 500,001 mg at 10,000,000. Non-positive or fractional input, weight 0, net ≤ 0. Repeat with other configured limits. |
| AC-05 | Anonymous, ineligible and foreign-owner requests. Restriction made effective between issuance and decision. Payments credential sending `SellGold`. |
| AC-06 | Force the first Ledger decision just before, at and after the 30 s boundary. Refresh requires a new quote and confirmation. No price means no quote. |
| AC-07, AC-08, AC-09 | Gold 500 vs 499 mg. Free liquidity 4,875,500 vs 4,875,499, checked against net, not gross. Liquidity drops between issuance and decision. Raising H or R lowers it by the same amount. A V01 gateway receivable adds nothing. Negative free liquidity rejects. A tampered snapshot is rejected with `QuoteTermsInvalid`. Each rejection has zero journal lines and unchanged balances. Suspense credit and reserved or restricted gold are NotRun until flows create them. |
| F02 under the guard | A capture with zero or negative free liquidity still credits once. Top-ups are never gated by liquidity. |
| AC-10, AC-11 | Independent connections with forced overlap: two sells from one customer's gold, and two customers against shared liquidity. Also a sell racing a V01 capture credit. Check loser reason, committed totals and non-negative gold. |
| AC-12 | 100 identical concurrent confirmations: identical receipts, one order, one decision, one journal. Same key with different content returns 409. A different key on a used quote creates no second sale. |
| AC-13 | Prove the Ledger commit, drop the reply, advance past expiry and restart Application. While unresolved the order is never shown as Completed or Rejected. The same result and amounts are recovered. Rollback before commit leaves nothing. An Application apply fault after commit is repaired without a second effect. |
| Regression and storage | Upgrade populated V01 and V02 stores and re-read their decisions, balances and wire shapes. Runtime is denied journal, projection and control-row DML. Invalid F05 legs are rejected by the deferred check. Full V01 and V02 suites pass. |

Record commands, exit codes, counts, per-case results and NotRun/Blocked items in `harness/evidence/003-sell-gold-wallet.md`. Keep static arithmetic and posting fixtures separate from runtime results. All V03 runtime checks are NotRun at the planning stage.
