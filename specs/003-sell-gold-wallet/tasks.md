---
description: "Implementation tasks for V03 sell gold for immediately usable IRR"
---

# Tasks — V03 sell gold for immediately usable IRR

**Input** — [Spec](spec.md), [plan](plan.md), [research](research.md), [data model](data-model.md), [V03 contract](contracts/v03.md) and [quickstart](quickstart.md).

**Tests** — The acceptance scenarios and Constitution XI require focused verification. Use synthetic data, real PostgreSQL and both HTTP hosts for persistence, concurrency and service-boundary claims.

**Organization** — Each user story has its own checkpoint. US1's first executable sale already includes every Ledger guard (gold, liquidity, expiry, limits) because they are decided in one transaction. US2 and US3 add boundary, contention and recovery evidence for the same path.

## Format

Use `- [ ] TNNN [P] [USn] Description with file path`. `[P]` marks disjoint work once its prerequisites are done. Paths are repository-relative. Let EF generate migration timestamps.

## Phase 1 — Setup

**Purpose** — Capture the V01/V02 baseline and extend the shared fixture.

- [X] T001 Run `dotnet build Avand.slnx --ignore-failed-sources` and the full `tests/Avand.V01.IntegrationTests/Avand.V01.IntegrationTests.csproj` suite. Record the command, exit code and V01/V02 test counts, or the environment blocker, in `harness/evidence/003-sell-gold-wallet.md`.
- [X] T002 Extend `tests/Avand.V01.IntegrationTests/Fixtures/V02Fixture.cs` (or add `Fixtures/V03Fixture.cs` on top of it). Add a controlled bid price and a helper that gives a customer gold through a real V01 top-up and V02 buy. Cash funding needs the T005/T006 schema and is added in T006.

## Phase 2 — Foundation

**Purpose** — Exact arithmetic, the liquidity-aware Ledger schema and dispatch. Blocks all stories.

- [X] T003 [P] Add `src/Shared/SellQuoteMath.cs` and link it from both host `.csproj` files. Implement weight input, gross-IRR input (weight floor), gross floor, fee ceiling, net, positive-net and inclusive gross limits with `Int128` and checked conversion. Policy `sell-v1` at 5,000 ppm, registered in both `TradingPolicy` classes (`src/Avand.Application.Api/Trading/BuyQuoteService.cs` and `src/Avand.Ledger.Api/Trading/TradingFinancialState.cs`) without changing `buy-v1`. Add independent oracle cases in `tests/Avand.V01.IntegrationTests/V03QuoteMathTests.cs` for AC-01, AC-01b and the AC-04 arithmetic (both limits, 1,000,000 → 999,999, 500,000/500,001 mg, zero and fractional input, net ≤ 0, configured rate).
- [X] T004 [P] Add migration and storage regression cases in `tests/Avand.V01.IntegrationTests/V03LedgerStorageTests.cs`. Upgrade populated V01+V02 stores and compare original journals, decisions and balances. Probe runtime denial of journal, projection and `LiquidityControl` DML, invalid F05 legs, mixed-asset imbalance and `OpeningCash` from the runtime role.
- [X] T005 Extend `src/Avand.Ledger.Api/Persistence/LedgerDbContext.cs` and add the owner-run V03 migration under `src/Avand.Ledger.Api/Persistence/Migrations/`. Add `BankCash` and `PlatformCapital` accounts, update `CK_Accounts_Asset`, add the `LiquidityControl` row (H, R = 0, version), `SellDecisions` and the sell business and quote claim tables.
- [X] T006 Extend the posting SQL (`V02JournalSql.cs` or a new `V03JournalSql.cs`) so `avand_post_journal` and the deferred template check accept exact F05 legs and fixture-only `OpeningCash`. Add `avand_lock_liquidity()` and `avand_free_liquidity()` (one statement per data-model.md) with scoped grants. Add `LockLiquidityAsync` and `ReadFreeLiquidityAsync` to `src/Avand.Ledger.Api/Persistence/LedgerLocks.cs`. Add fixture-only `OpeningCash` funding of `BankCash`, a free-liquidity reader and an H/R setter to the test fixture, all behind fixture authority. Run T004.
- [X] T007 Make V01 capture take the liquidity guard after the producer lock and before eligibility in `src/Avand.Ledger.Api/Payments/GatewayCapturePosting.cs`. Capture never rejects on liquidity. Add a case to `V03LedgerStorageTests.cs` where a capture with zero or negative free liquidity still credits once. Run the full V01 and V02 suites to confirm no regression or deadlock.
- [X] T008 Add `SellGold` dispatch for the `Application.Trading` producer in `src/Avand.Ledger.Api/Payments/LedgerPaymentEndpoints.cs` and `src/Avand.Ledger.Api/LedgerHost.cs`, with producer-scoped inquiry. Add wrong-scope cases (Payments sending `SellGold`) to `tests/Avand.V01.IntegrationTests/V03LedgerScopeTests.cs`.

**Checkpoint** — The schema upgrades cleanly, V01 and V02 pass, and runtime privileges are verified.

## Phase 3 — User Story 1 (P1) — Quote, confirm and see the posted sale

**Goal** — An eligible customer sees the sell price, fee and net, confirms once, and sees the order and balances. The net is immediately usable.

**Independent test** — Give a customer gold and fund liquidity, sell 500 mg, compare the displayed terms with the single F05 journal and both balance deltas, then spend the net on a V02 buy. Covers AC-01–05.

### Tests

- [X] T009 [P] [US1] Add HTTP contract cases in `tests/Avand.V01.IntegrationTests/V03CustomerContractTests.cs` for `sell-quotes` (both input modes, exactly-one-of validation, limits, net ≤ 0, unavailable price), `sell-orders` (202, strict body, status location) and the owned order read (AC-01, AC-01b, AC-04, AC-05).
- [X] T010 [P] [US1] Add the journey in `tests/Avand.V01.IntegrationTests/V03SellJourneyTests.cs`: exact gold −500 mg and IRR +4,875,500, separate fee line, balanced F05, net in `CustomerRialAvailable`, then a V02 buy funded only by the net. Add ineligible, foreign-owner and restriction-before-decision cases (AC-02, AC-03, AC-05).

### Implementation

- [X] T011 [P] [US1] Add `SellQuote`, `SellOrder` and `SellRecoveryWork` to `src/Avand.Application.Api/Persistence/ApplicationDbContext.cs` with an owner-run migration. Unique customer/key and quote, one work item per order, frozen request and operation ID.
- [X] T012 [P] [US1] Implement `src/Avand.Ledger.Api/Trading/SellGold.cs` and `src/Avand.Ledger.Api/Trading/LiquidityState.cs`. Replay first, then lock backing → liquidity → eligibility → accounts. Sample time after locks and evaluate in the data-model order. Post F05, store claims and the decision in one transaction, with no effects on rejection.
- [X] T013 [US1] Extend `src/Avand.Application.Api/Trading/LedgerTradingClient.cs` with `SellGold` send and inquiry under the Trading credential. Treat transport ambiguity as unknown.
- [X] T014 [US1] Implement `src/Avand.Application.Api/Trading/SellQuoteService.cs` using the bid side of `PairedPriceSource`, the V02 provider-age rule, `SellQuoteMath`, pinned policy and a 30-second lifetime. Fail closed on invalid price or policy. Never disclose platform liquidity.
- [X] T015 [US1] Implement atomic admission in `src/Avand.Application.Api/Trading/SellOrderAdmission.cs`. Persist the receipt, key and quote claims, frozen request and work before dispatch. Exact replay is resolved before the quote-reuse conflict.
- [X] T016 [US1] Implement send and terminal apply in `src/Avand.Application.Api/Trading/SellOrderDelivery.cs` and the endpoints in `src/Avand.Application.Api/Trading/SellEndpoints.cs`, registered in `src/Avand.Application.Api/ApplicationHost.cs`. Map `InsufficientLiquidity` to the safe "sales temporarily unavailable" reason. Run T009 and T010 on real hosts.

**Checkpoint** — US1 journey passes on real PostgreSQL and both hosts.

## Phase 4 — User Story 2 (P1) — Expiry, insufficient gold and insufficient liquidity

**Goal** — A sale whose conditions fail has no financial effect and shows its reason.

**Independent test** — With controlled clocks, gold and liquidity, cross each boundary separately. Covers AC-06–09.

- [X] T017 [P] [US2] Add `tests/Avand.V01.IntegrationTests/V03QuoteBoundaryTests.cs`: first Ledger decision before, at and after 30 s, refresh needing a new quote and confirmation, no price meaning no quote (AC-06).
- [X] T018 [P] [US2] Add `tests/Avand.V01.IntegrationTests/V03RejectionTests.cs`: gold 500 vs 499 mg, free liquidity 4,875,500 vs 4,875,499 (net, not gross), liquidity dropping between issuance and decision, a higher H or R lowering liquidity by the same amount, a V01 gateway receivable adding nothing, negative liquidity rejecting, and a snapshot whose gross, fee or net doesn't match recomputation (`QuoteTermsInvalid`). Assert zero journal lines and unchanged balances for each, plus the stored reason (AC-07–09, edge cases). Record as NotRun, with reason: suspense credit (no suspense posting exists until V08) and reserved or restricted gold (no flow creates them yet). `SellGold` reads only `CustomerGoldAvailable`.
- [X] T019 [US2] Expose the expired-order refresh action with the original input mode and value in `src/Avand.Application.Api/Trading/SellEndpoints.cs`. Fix any guard-order gaps T017 and T018 reveal in `SellGold.cs`. Record which guard caused each rejection in `harness/evidence/003-sell-gold-wallet.md`.

**Checkpoint** — Every rejection path is proven to have zero financial effect.

## Phase 5 — User Story 3 (P1) — Concurrency, idempotency and lost responses

**Goal** — Races never exceed gold or liquidity, and repeats or lost replies never cause a second sale.

**Independent test** — Forced overlap on independent connections, a 100-request burst and injected faults. Covers AC-10–13.

- [X] T020 [P] [US3] Add `tests/Avand.V01.IntegrationTests/V03ConcurrencyTests.cs`: two sells against one customer's gold, two customers against shared liquidity, and a sell racing a V01 capture credit. Assert the loser reason, non-negative gold and total net ≤ initial free liquidity (AC-10, AC-11).
- [X] T021 [P] [US3] Add `tests/Avand.V01.IntegrationTests/V03IdempotencyTests.cs`: 100 identical concurrent confirmations produce one order, one decision and one journal. Same key with other content returns 409. Another key on a used quote creates no second sale (AC-12).
- [X] T022 [P] [US3] Add `tests/Avand.V01.IntegrationTests/V03RecoveryTests.cs` with fault hooks in the fixture: commit then drop the reply, restart Application past expiry, rollback before commit, and an Application apply fault after commit. While unresolved, the order reads `Processing` or `FinancialOutcomeUnknown`, never `Completed` or `Rejected`, and an unreachable Ledger shows balances as unavailable, not zero (AC-13, edge cases).
- [X] T023 [US3] Implement `src/Avand.Application.Api/Trading/SellRecoveryWorker.cs` with the V02 lease and version pattern, registered in `ApplicationHost.cs`. Resume after restart, inquire and resend with the original identity, and back off on transient failures.
- [X] T024 [US3] Complete replay-safe apply and work completion in `SellOrderDelivery.cs`. Run T020–T022 and record both-store reads after each fault in `harness/evidence/003-sell-gold-wallet.md`.

**Checkpoint** — Races, the burst and recovery cases pass.

## Phase 6 — Cross-cutting verification

- [X] T025 [P] Correlate order ID, operation ID, journal and recovery state in logs from `SellRecoveryWorker.cs` and `SellGold.cs`, without credentials, raw evidence or unnecessary customer data. Never log platform liquidity values at customer-facing level.
- [X] T026 [P] Review the authorization, migration and posting boundaries against `contracts/v03.md`: separate Payments and Trading permissions, owner-only migrations, no Application access to Ledger tables, and every liquidity-reducing writer holding the guard.
- [X] T027 Run the build, focused V03 suite and V01/V02 regression from `quickstart.md`. Record commands, exit codes, counts, per-AC results and NotRun/Blocked items (including the V04 withdrawal reservation) in `harness/evidence/003-sell-gold-wallet.md`. Update this file from observed evidence.

## Dependencies & Execution Order

| Tasks | Prerequisites |
| --- | --- |
| T001 → T002 | Baseline before fixture changes |
| T003, T004 | T002 |
| T005 → T006 → T007 → T008 | T003 and T004. T007 must pass V01/V02 before continuing. |
| T009, T010 | T008 |
| T011, T012 | T009 and T010 written. Application and Ledger proceed independently. |
| T013 | T012 |
| T014 → T015 → T016 | T011 and T013 |
| T017, T018 → T019 | T016 |
| T020, T021, T022 → T023 → T024 | T019 |
| T025, T026 → T027 | T024 |

Story order is US1 → US2 → US3. Each checkpoint uses its own test data.

## Parallel Examples

- **Foundation** — T003 (pure math) and T004 (storage tests) touch different files.
- **US1** — After T009/T010, T011 (Application storage) and T012 (Ledger decision) advance independently.
- **US3** — T020, T021 and T022 are separate test files. Only T022 changes fixture fault hooks.

## Implementation Strategy

1. Run [Analyze](../../docs/SDD_HARNESS.md#52-analyze-before-implement) before implementing. Load `avand-ledger-correctness`, `avand-operation-recovery`, `avand-financial-verification` and `supabase-postgres-best-practices`, plus the API security skill for T009/T016/T026.
2. T007 changes V01 code. Treat a V01/V02 regression failure there as blocking.
3. Use US1 as the first demonstration, then add US2 and US3. All three are P1 and required for V03 acceptance.
4. A build, a static fixture or an empty test filter is not runtime evidence. Keep task status in step with `harness/evidence/003-sell-gold-wallet.md`.
