---
description: "Implementation tasks for V02 wallet-funded gold purchase"
---

# Tasks — V02 wallet-funded gold purchase

**Input** — [Spec](spec.md), [plan](plan.md), [research](research.md), [data model](data-model.md), [V02 contract](contracts/v02.md) and [quickstart](quickstart.md).

**Tests** — The approved acceptance scenarios and Constitution XI require focused verification. Use synthetic data and real PostgreSQL and HTTP hosts for persistence and service-boundary claims.

**Organization** — Each user story has its own checkpoint. The first executable purchase path includes Ledger guards and atomic result application. Later phases add customer-facing guidance, fault recovery and evidence for the same path.

## Format

Use `- [ ] TNNN [P] [USn] Description with file path`. The optional `[P]` identifies disjoint work once its listed prerequisites are complete. Paths are repository-relative. New source filenames identify the intended owner and landing point. Let EF generate migration timestamps.

## Phase 1 — Setup

**Purpose** — Establish the V01 regression baseline and reuse its test infrastructure.

- [X] T001 Run the repository build and existing V01 integration tests using `Avand.slnx` and `tests/Avand.V01.IntegrationTests/Avand.V01.IntegrationTests.csproj`. Record command, exit code, test counts or environment blocker in `harness/evidence/002-buy-gold-wallet.md`. Use Docker or the configured disposable PostgreSQL override described in `specs/002-buy-gold-wallet/quickstart.md`.
  - The fallback build passed and the resumed V01 suite passed 15/15. [Observed evidence](../../harness/evidence/002-buy-gold-wallet.md#t001-observed-checks)
- [X] T002 Add `tests/Avand.V01.IntegrationTests/Fixtures/V02Fixture.cs` and reuse the disposable PostgreSQL and real HTTP host setup from `tests/Avand.V01.IntegrationTests/Fixtures/PostgreSqlFixture.cs` and `tests/Avand.V01.IntegrationTests/Fixtures/V01Fixture.cs`. Provide independently controlled server clocks, a deterministic paired-price fixture and distinct customer and service identities. Keep test-only custody setup behind fixture access and fund a customer through actual V01 top-up.

## Phase 2 — Foundation

**Purpose** — Establish exact shared calculations, protected Ledger storage and typed scope before a buy can reach either service.

- [X] T003 [P] Add migration and storage regression cases in `tests/Avand.V01.IntegrationTests/V02LedgerStorageTests.cs`. Upgrade populated V01 stores and compare original journal IDs, decisions, amounts and balances. Probe denied runtime DML, append-only history, mixed-asset imbalance, invalid template legs and projection mismatch. Use controlled fixture authority for invariant violations distinct from permission checks.
- [X] T004 [P] Add `src/Shared/BuyQuoteMath.cs` and link it from `src/Avand.Application.Api/Avand.Application.Api.csproj` and `src/Avand.Ledger.Api/Avand.Ledger.Api.csproj`. Use checked exact intermediates for whole-mg floor, whole-IRR principal floor, fee ceiling and debit. Validate configurable policy with initial 1% buy fee and inclusive 1,000,000–5,000,000,000 IRR limits on final pre-fee principal. Add independent arithmetic cases in `tests/Avand.V01.IntegrationTests/V02QuoteMathTests.cs` for fractional principal, zero fee, both limits and out-of-range input.
- [X] T005 Extend `src/Avand.Ledger.Api/Persistence/LedgerDbContext.cs` and add the owner-run V02 migration under `src/Avand.Ledger.Api/Persistence/Migrations/`. Backfill IRR/F02, preserve row identities and wire field names, rename internal amounts to neutral minor units, and add account asset and normal side, journal template, backing evidence/guard, buy outcomes and unique business and quote claims. Update SQL readers, account constraints and synthetic balanced custody setup in `tests/Avand.V01.IntegrationTests/Fixtures/V02Fixture.cs`.
- [X] T006 Add the controlled F02/F04 posting boundary and deferred template, per-asset balance and signed projection checks in `src/Avand.Ledger.Api/Persistence/Migrations/`. Route V01 capture through it in `src/Avand.Ledger.Api/Payments/GatewayCapturePosting.cs` and update fixture grants in `tests/Avand.V01.IntegrationTests/Fixtures/V01Fixture.cs`. Preserve capture replay and restricted credits. Grant scoped execution and protected reads/locks while removing direct runtime financial DML. Run T003 and the V01 regression before proceeding.
- [X] T007 Prepare a single scoped internal dispatcher in `src/Avand.Ledger.Api/Payments/LedgerPaymentEndpoints.cs` and `src/Avand.Ledger.Api/LedgerHost.cs`. Preserve V01 typed capture, controls, lookup and balance responses. Add a distinct Trading credential, strict operation-type dispatch and producer-owned inquiry. Verify wrong-scope and V01 compatibility cases in `tests/Avand.V01.IntegrationTests/V02LedgerScopeTests.cs`.

## Phase 3 — User Story 1 (P1) — Quote, confirm and see the posted purchase

**Goal** — An eligible customer buys with the existing spendable wallet and reads the order and authoritative balances.

**Independent test** — Post one V01 top-up, issue a buy quote, confirm once and compare the displayed terms, one posted F04 journal and both committed balance deltas. Test exact and insufficient funding, amount limits and ownership. Covers AC-01–06.

### Tests

- [X] T008 [P] [US1] Add HTTP customer contract cases in `tests/Avand.V01.IntegrationTests/V02CustomerContractTests.cs`. Exercise all four public V02 interfaces. Assert 201 and exact quote amounts, initial and configured fee, inclusive final principal limits, invalid input, 202 with an owned status location, strict confirmation shape and owned status and balance responses (AC-01–03, AC-06).
- [X] T009 [P] [US1] Add the V01-funded V02 journey and rejection cases in `tests/Avand.V01.IntegrationTests/V02BuyJourneyTests.cs`. Assert wallet debit, fee, posted gold, per-asset balance and durable result. Test one rial short, effective eligibility changing before first decision and unavailable balance reads (AC-04–06).

### Implementation

- [X] T010 [P] [US1] Add buy quote, order, recovery work and procurement intent records in `src/Avand.Application.Api/Persistence/ApplicationDbContext.cs` and an owner-run migration under `src/Avand.Application.Api/Persistence/Migrations/`. Add immutable snapshot and request identity, uniqueness for customer/key and quote, versioned recovery lease and one procurement intent per order.
- [X] T011 [P] [US1] Implement the protected first decision, posting and replay in `src/Avand.Ledger.Api/Trading/BuyGold.cs` and `src/Avand.Ledger.Api/Persistence/LedgerLocks.cs`. Lock operation identity, shared backing, effective eligibility and accounts in a stable order. Replay stored results before current guards, then sample first-decision time after locks. Enforce exact immutable terms, effective limits, total wallet funding and normal/outage backing with one-time deduction of outgoing commitments. Store posted incremental deficit, claims, journal and projections with the outcome in one transaction. Reject invalid or unavailable capacity without effects.
- [X] T012 [US1] Wire F04, scoped result lookup and authoritative trade financial state through `src/Avand.Ledger.Api/Payments/LedgerPaymentEndpoints.cs` and `src/Avand.Ledger.Api/Trading/TradingFinancialState.cs`. Expose committed balances and advisory inputs without disclosing other customer claims. Verify existing V01 responses.
- [X] T013 [US1] Implement the Trading HTTP client in `src/Avand.Application.Api/Trading/LedgerTradingClient.cs`. Send frozen typed operations, resolve outcomes under the original ID, and translate scoped balances, eligibility and retryable failures. Use a distinct Trading credential with no fallback to Payments.
- [X] T014 [US1] Implement paired-price input and immutable quote issuance in `src/Avand.Application.Api/Trading/BuyQuoteService.cs` and `src/Avand.Application.Api/Trading/PairedPriceSource.cs`. Use the synthetic development adapter and validate provider time age in inclusive 0–30 seconds. Apply exact buy-side arithmetic and effective final pre-fee limits, pin the quoted policy, and issue a separate 30-second authorization. Fail closed on invalid price or policy.
- [X] T015 [US1] Implement atomic buy admission in `src/Avand.Application.Api/Trading/BuyOrderAdmission.cs`. Persist the original receipt, customer/key and quote claims, frozen request, permanent OperationId and recovery work before dispatch. Resolve exact replay before quote-reuse conflict and return 202 for the same order.
- [X] T016 [US1] Implement the send and terminal apply path in `src/Avand.Application.Api/Trading/BuyOrderDelivery.cs`. Use the stored request and treat transport ambiguity as unresolved. Apply the original terminal outcome with any positive procurement intent in the same local commit from the Ledger's stored increment. Expose owned quote, confirmation, order and balance endpoints through `src/Avand.Application.Api/Trading/BuyEndpoints.cs` and `src/Avand.Application.Api/ApplicationHost.cs`. Run T008 and T009 on the real hosts.

## Phase 4 — User Story 2 (P1) — Refresh expired quotes and protect shared resources

**Goal** — Customers see advisory availability and can explicitly confirm a new quote after expiry. Distinct purchases cannot exceed the wallet or pool.

**Independent test** — Run with controlled clocks, seeded eligible custody and separate concurrent orders. At expiry reject the first decision and require a new confirmation. Exercise wallet and backing contention separately and compare committed totals. Covers AC-07–12.

### Tests

- [X] T017 [P] [US2] Add timing and replacement cases in `tests/Avand.V01.IntegrationTests/V02QuoteBoundaryTests.cs`. Test source age and quote expiry on opposite sides of each boundary, invalid provider times, a still-valid quote after feed failure, and a blocked first Ledger decision that crosses expiry. Verify no charge until the refreshed quote receives a new confirmation (AC-07–08).
- [X] T018 [P] [US2] Add independent-session wallet and backing races in `tests/Avand.V01.IntegrationTests/V02CapacityTests.cs`. Force actual overlap with distinct keys and quotes. Keep unrelated guards permissive. Assert loser reason, aggregate deltas, nonnegative wallet, normal and outage boundaries, one-time active-outflow deduction and no capacity from unconfirmed receipts. Cover evidence unavailable, advisory limits and no resizing (AC-09–12).

### Implementation

- [X] T019 [US2] Add exact fee-inclusive advisory max and unavailable or below-minimum responses in `src/Avand.Application.Api/Trading/BuyQuoteService.cs`. Use authoritative trade financial state and the discrete weight calculation in `src/Shared/BuyQuoteMath.cs`. Do not reserve capacity or change a confirmed order to fit.
- [X] T020 [US2] Expose the expired-order refresh action with original requested principal in `src/Avand.Application.Api/Trading/BuyEndpoints.cs`. A replacement quote gets a new identity and requires explicit confirmation. Run T017 and T018 on real PostgreSQL and both HTTP hosts and record which guard caused each rejection in `harness/evidence/002-buy-gold-wallet.md`.

## Phase 5 — User Story 3 (P1) — Recover the original order and procurement intent

**Goal** — Duplicate requests and outages recover a single financial outcome and the exact incremental follow-up.

**Independent test** — Burst 100 identical confirmations, then separately lose the response after commit and restart Application after expiry. Count original durable effects and exercise covered, initial-deficit and incremental-deficit cases. Covers AC-13–16.

### Tests

- [X] T021 [P] [US3] Add `tests/Avand.V01.IntegrationTests/V02IdempotencyTests.cs` with 100 same-key confirmations and identical receipts. Count one order, one Ledger outcome, one journal and exact balance deltas. Check changed-content conflict and quote reuse under a different key (AC-13).
- [X] T022 [P] [US3] Add `tests/Avand.V01.IntegrationTests/V02RecoveryTests.cs` and extend targeted faults in `tests/Avand.V01.IntegrationTests/Fixtures/V02Fixture.cs`. Prove commit before losing the reply, restart Application after expiry, and separately inject precommit Ledger rollback and postcommit Application apply failure. Verify original-ID replay including durable rejection, unresolved NotFound, and zero/initial/incremental procurement counts and amounts (AC-14–16).

### Implementation

- [X] T023 [US3] Implement leased buy work in `src/Avand.Application.Api/Trading/BuyRecoveryWorker.cs` and register it in `src/Avand.Application.Api/ApplicationHost.cs`. Reuse the V01 lease/version pattern with order-owned work. Resume dispatch after restart, inquire and retry with the original identity and frozen request, and reschedule transient failures with bounded backoff.
- [X] T024 [US3] Complete replay-safe result application and work completion in `src/Avand.Application.Api/Trading/BuyOrderDelivery.cs`. Resolve overlapping attempts without a second financial effect or procurement insert. Keep missing or ambiguous results recoverable. Repair local status from the durable Ledger result without repricing or contacting the custodian.
- [X] T025 [US3] Run T021 and T022 via `tests/Avand.V01.IntegrationTests/Avand.V01.IntegrationTests.csproj`. Read both stores after each injected fault, verify one effect and the exact procurement delta, and record the observed results in `harness/evidence/002-buy-gold-wallet.md`.

## Phase 6 — Cross-cutting verification

- [X] T026 [P] Correlate order ID, operation ID, journal reference and recovery state in `src/Avand.Application.Api/Trading/BuyRecoveryWorker.cs` and `src/Avand.Ledger.Api/Trading/BuyGold.cs`. Make unresolved delivery visible while omitting credentials, raw evidence and unnecessary customer data.
- [X] T027 [P] Check the authorization, migration and posting boundaries against `specs/002-buy-gold-wallet/contracts/v02.md`, `src/Avand.Ledger.Api/Persistence/Migrations/` and `tests/Avand.V01.IntegrationTests/V02LedgerScopeTests.cs`. Confirm the Payments and Trading permissions are distinct, migrations use owner credentials and Application never accesses Ledger tables directly.
- [X] T028 Run the build, focused V02 suite and V01 regression from `specs/002-buy-gold-wallet/quickstart.md` against real PostgreSQL. Record commands, exit codes, selected test counts, per-scenario results and any NotRun/Blocked checks in `harness/evidence/002-buy-gold-wallet.md`. Update this `specs/002-buy-gold-wallet/tasks.md` from observed evidence.

## Dependencies & Execution Order

| Tasks | Prerequisites |
| --- | --- |
| T001 → T002 | V01 baseline before fixture changes |
| T003, T004 | T002 |
| T005 → T006 → T007 | T003 and T004 before shared Ledger migration and dispatch |
| T008, T009 | T007. Capture a meaningful failing case where practical. |
| T010, T011 | T008 and T009 authored. The Application and Ledger changes can proceed independently. |
| T012 → T013 | T011 |
| T014 → T015 → T016 | T010, T012 and T013 before the Application journey and the T008–T009 checkpoint |
| T017, T018 → T019 → T020 | T016 |
| T021, T022 → T023 → T024 → T025 | T020 |
| T026, T027 → T028 | T025 |

Story order is US1 → US2 → US3. US2 exercises the shared Ledger controls already required by US1. US3 extends the same durable admission and application path with automated restart recovery. Each checkpoint uses its own test data and does not rely on another story's test effects.

## Parallel Examples

- **US1** — Author T008 and T009 in separate test files. After that baseline, T010 Application storage and T011 Ledger execution can advance independently.
- **US2** — Author T017 clock/replacement cases and T018 contention/capacity cases in separate test files after US1.
- **US3** — Author T021 duplicate-confirmation cases and T022 recovery cases after US2. T021 uses the existing fixture surface while T022 adds targeted fault controls.

## Implementation Strategy

1. Run [Analyze](../../docs/SDD_HARNESS.md#52-analyze-before-implement) before implementation. Load the project skills named in `AGENTS.md` for each affected slice.
2. Use US1 as the first local demonstration. Verify the V01-funded purchase with exact effects and first-decision financial protections.
3. Add US2 and US3, then run the full V02 matrix and V01 regression. All three stories are P1 and required for V02 acceptance. The first demonstration is an intermediate checkpoint.
4. Keep the feature's runtime evidence and task status together. A static check, a successful build or an empty test filter supplies no PostgreSQL or recovery evidence.
