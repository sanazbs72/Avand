# V03 sell gold for immediately usable IRR verification evidence

**Date** — 2026-11-08  
**Feature** — `003-sell-gold-wallet`  
**Source revision** — `80d29f6` plus uncommitted V01/V02 working tree (no V03 code at T001)

## Pre-implementation review

The requirements checklist passed. Analyze found six issues (A1–A6), resolved them in the owning artifacts, and the rerun was clean. See [checklist](../../specs/003-sell-gold-wallet/checklists/requirements.md#analyze--2026-11-08).

## T001 baseline

| Command | Exit | Result |
| --- | --- | --- |
| `dotnet --version`; `docker info --format '{{.ServerVersion}}'` | 0 | .NET SDK 10.0.403, Docker Engine 27.5.1. `AVAND_V01_TEST_POSTGRES` unset; Testcontainers PostgreSQL 17.6 used. |
| `dotnet build Avand.slnx --ignore-failed-sources` | 0 | Both hosts and the integration test assembly built with 0 warnings and 0 errors. |
| `dotnet test tests/Avand.V01.IntegrationTests/Avand.V01.IntegrationTests.csproj --no-build --verbosity minimal` | 0 | 54 passed, 0 failed, 0 skipped in 6 m 25 s against real PostgreSQL and both HTTP hosts. |
| `dotnet test ... --no-build --list-tests` | 0 | 54 discovered: 15 V01 and 39 V02-named tests. This is the regression baseline for T007 and T027. |

**T001 — Pass.**

## T002 fixture

Added `tests/Avand.V01.IntegrationTests/Fixtures/V03Fixture.cs` (derives from `V02Fixture`) and `V03FixtureTests.cs`. `V02Fixture` was changed from `sealed` to non-sealed so V03 can inherit its hosts, clocks, price control and fault hooks. There is no behavior change. The fixture sets the B07 paired price (ask 10,000,000, bid 9,800,000 IRR/g) and gives a customer gold through a real V01 top-up and a real V02 buy. Bank-cash funding is deferred to T006, after Analyze finding A2.

| Command | Exit | Result |
| --- | --- | --- |
| First `dotnet build Avand.slnx --ignore-failed-sources` | 1 | CS0509: `V02Fixture` was sealed. The focused test run that followed selected 0 tests, which is not evidence. |
| `dotnet build Avand.slnx --ignore-failed-sources` after unsealing | 0 | 0 warnings, 0 errors. |
| `dotnet test ... --no-build --filter "FullyQualifiedName~V03"` | 0 | 1 passed in 12 s on PostgreSQL 17.6 and both hosts. Customer got exactly 500 mg and Other exactly 100 mg, wallets unchanged, through real top-up and buy. |
| `dotnet test ... --no-build --filter "FullyQualifiedName~V02FixtureTests\|FullyQualifiedName~V02BuyJourneyTests"` | 0 | 5 passed, so V02 still works after unsealing. The full V01/V02 regression reruns at T007 and T027. |

**T002 — Pass.**

## Phase 2 — Foundation (T003–T008)

Changes: `src/Shared/SellQuoteMath.cs` (linked into both hosts and tests); a `sell-v1` policy (5,000 ppm) in both `TradingPolicy` classes; the Ledger model and migration `20261108034544_V03SellLiquidity` (`BankCash`, `PlatformCapital`, `LiquidityControl`, `SellDecisions` plus claims, `CK_Accounts_Asset`); `V03JournalSql` (F05 and `OpeningCash` shapes, F05 durable-decision check, `avand_post_journal` accepting F05 but not `OpeningCash`, `avand_lock_liquidity()`, `avand_free_liquidity()`, grant upgrade); `LockLiquidityAsync`/`ReadFreeLiquidityAsync`; `GatewayCapturePosting` takes the liquidity guard after the producer lock and before eligibility; `SellGold` dispatch and inquiry. The migration was generated with dotnet-ef 10.0.13, installed from the local NuGet cache into the session scratchpad (not added to the repo).

| Task | Command | Exit | Result |
| --- | --- | --- | --- |
| T003 | `dotnet test ... --no-build --filter "FullyQualifiedName~V03QuoteMathTests\|FullyQualifiedName~V02QuoteMathTests"` | 0 | 22 passed (13 V03, 9 V02). Static arithmetic layer: AC-01, AC-01b, AC-04 values, configured rate and limits, invalid and overflow input. |
| T004 | `dotnet test ... --filter "FullyQualifiedName~V03LedgerStorageTests"`, before T005 | 1 | 5 failed as expected (`LiquidityControl` / `avand_free_liquidity` did not exist). `F05WithWrongLegsIsRejected` already passed, because unknown templates were rejected. |
| T005 | First V03 run after the migration | 1 | All failed with `55006: cannot ALTER TABLE "Accounts" because it has pending trigger events`: EF added `CK_Accounts_Asset` after the seed insert. Fixed by moving the constraint ahead of `InsertData`. |
| T005–T006 | `dotnet test ... --filter "FullyQualifiedName~V03LedgerStorageTests\|FullyQualifiedName~V03FixtureTests"` | 0 | 7 passed on PostgreSQL 17.6: upgrade of a populated V01+V02 store keeps history and balances, and leaves free liquidity −1 (receivable is not cash); runtime role upgraded; runtime denied `LiquidityControl`/`Journals`/`Accounts`/`SellDecisions` DML; runtime `OpeningCash` rejected; fixture cash raises liquidity, a top-up lowers it, H and R lower it; invalid F05 legs and an F05 with no `SellDecision` rejected. |
| T007 | Same filter with the two new T007 cases | 0 | 8 passed. A capture with free liquidity < 0 still credited once (liquidity −1,000,000). A capture blocked behind an independently held liquidity guard, with the wallet unchanged, then completed after release. |
| T007 | Full suite `dotnet test ... --no-build` (first run) | 1 | 75 passed, 1 failed: `V02IdempotencyTests.HundredIdenticalConfirmationsProduceOneOrderAndOneFinancialEffect`. 1 of 100 Application `POST /buy-orders` replies was HTTP 500. |
| T007 | `--filter "FullyQualifiedName~V02IdempotencyTests"` ×3 after rebuild | 0 ×3 | 2/2 passed each time. The failure is intermittent. The Application admission path was not changed in V03. A log-capture diagnosis was stopped at the user's request, so the root cause is **NotRun**. Hypothesis: connection exhaustion under a 100-request burst. See Open items. |
| T008 | `--filter "FullyQualifiedName~V03"` | 0 | 25 passed: Payments sending `SellGold` → 403, anonymous → 401, wrong side or unknown field → 400, inquiry producer-scoped (404 for unknown). |
| T007/T008 | Full suite `dotnet test ... --no-build` (final) | 0 | **79 passed**, 0 failed, 0 skipped in 9 m 28 s (15 V01, 39 V02, 25 V03). |

**T003–T008 — Pass.** `SellGold.ExecuteAsync` returns 501 for a valid envelope until T012. No sale can post yet.

## Open items

- **V02 burst 500 (intermittent).** Seen once in 4 full or focused runs of the V02 100-confirmation test. A 500 on an idempotent financial endpoint should not happen even under load. Investigate separately: capture Application logs during the burst and check PostgreSQL `max_connections` against the pool size.

## T009–T027 — implementation and verification (2026-11-08)

**Implementation**
- **Already present at Phase 3 start:** `SellQuoteService`, `SellOrderAdmission`, `SellOrderDelivery`, `SellRecoveryWorker`, `SellEndpoints`, the `V03SellOrders` Application migration, the Ledger `SellGold` (lock order backing → liquidity → eligibility → accounts, data-model guard order, F05 posting) and the `SellGold` client.
- **Added in this pass:**
  - The T019 expired-order `refresh` action, which carries the original `quantityMg` or `requestedGrossIrr`.
  - Transient-database handling in `SellOrderAdmission`, as in buy admission (503 `OrderAdmissionUnavailable`, retryable).
  - The V03 fault hooks in `V03Fixture`: a Ledger precommit fault on `SellDecisions` and an Application apply fault on `SellOrders`.
- **T025 (logs):** sell logs carry only order, work, operation and journal IDs, the reason and the error type. The Application never reads or logs a liquidity value.
- **T026 (boundaries):** Payments sending `SellGold` hits the denied dispatch branch (`V03LedgerScopeTests`). Migrations remain owner-run, the runtime role has no Ledger table DML (`V03LedgerStorageTests`), and the Application uses only Ledger HTTP.

**New test classes**

| Class | Covers |
| --- | --- |
| `V03QuoteBoundaryTests` (T017) | AC-06: decision at expiry −1 µs posts and at expiry rejects with `QuoteExpired`. A weight or gross refresh repeats the original input and needs a new quote and confirmation. No or invalid price gives 503 `PriceUnavailable`. |
| `V03RejectionTests` (T018) | AC-07–09: 500 mg vs 499 mg `InsufficientGold`, then 499 mg posts. Liquidity 4,875,499 rejects and exactly 4,875,500 (net, not gross) posts leaving 0. H or R lowers liquidity by exactly the change. A drop between issuance and decision rejects, and negative liquidity rejects. A V01 receivable adds nothing (a credit lowers liquidity 1:1). A fee mismatch gives `QuoteTermsInvalid`. Each rejection: no journal, a stored reason, unchanged gold, wallet and journal count. |
| `V03ConcurrencyTests` (T020) | AC-10–11: two sells of one customer's gold give one `Completed` and one `InsufficientGold`, gold 0. Two customers with liquidity for one net give one post and one `SalesTemporarilyUnavailable`. A sale racing a capture credit: the capture credits once, and liquidity matches whichever order took the guard. |
| `V03IdempotencyTests` (T021) | AC-12: 100 identical confirmations give one receipt, order, decision and journal. Same key with another quote gives 409, and another key on a used quote gives 409. |
| `V03RecoveryTests` (T022) | AC-13: commit then lost reply, Application restart after expiry, recovered with `decidedAt` before expiry and one decision. Unanswered send stays `FinancialOutcomeUnknown` and balances read 503, never 0. Ledger precommit fault leaves no decision, claim, journal or balance change. Application apply fault after commit stays unresolved, then repairs forward with one journal, and a frozen replay is `Posted` with no new journal. |

**Runs**

| Command | Exit | Result |
| --- | --- | --- |
| `dotnet build Avand.slnx --ignore-failed-sources` | 0 | 0 warnings, 0 errors |
| `dotnet test ... --filter "FullyQualifiedName~V03\|FullyQualifiedName~V02IdempotencyTests"` (`v03-phase3.trx`) | 1 | 32 failed during fixture initialization: Docker engine HTTP 500 (`DockerUnavailableException`). **Blocked**, then Docker Desktop was restarted. |
| Same filter (`v03-all.trx`) | 1 | 59 passed, 1 failed. All V03 classes passed. The one failure was the first version of the V02 53300 regression; see the 002 evidence. |
| `dotnet test ... --no-build` full suite (`v03-full.trx`) | 1 | 112 total, **111 passed**, 1 failed in 7 m 5 s. `V02LedgerScopeTests` failed in fixture init (`V01Fixture.cs:76`) with "connection aborted": a pooled admin session to the previous, disposed container reused by an identical connection string (same host port). This is test-environment only. Fixed: `PostgreSqlFixture.DisposeAsync` now clears Npgsql pools. |
| `--filter "V02LedgerScopeTests\|V02FixtureTests\|V03LedgerScopeTests"` after the fix (`v03-rerun.trx`) | 0 | 5 passed |

**T027 — Pass with notes**
- **Full-suite rerun after the pool-clearing fix:** NotRun. The affected classes were rerun.
- **NotRun by design:** suspense credit (no suspense posting before V08), reserved or restricted gold (no flow creates them yet; `SellGold` reads only `CustomerGoldAvailable`), and the V04 withdrawal reservation.
