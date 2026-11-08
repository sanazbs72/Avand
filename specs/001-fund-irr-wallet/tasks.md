# Tasks: V01 IRR wallet top-up and result

**Input** — [Spec](spec.md), [plan](plan.md), [research](research.md), [data model](data-model.md), [contracts](contracts/v01.md), and [quickstart](quickstart.md).

## Format

Each task has an ID and a repository-relative path. [P] marks tasks that can run alongside the identified peer once their prerequisites are complete. Story labels map to the three P1 stories in the spec.

## Phase 1: Setup

- [X] T001 Add compatible EF Core 10 and Npgsql dependencies to `src/Avand.Application.Api/Avand.Application.Api.csproj` and `src/Avand.Ledger.Api/Avand.Ledger.Api.csproj`. Keep the hosts independent.
- [X] T002 [P] Add the .NET 10 xUnit and Testcontainers PostgreSQL project at `tests/Avand.V01.IntegrationTests/Avand.V01.IntegrationTests.csproj` and register it in `Avand.slnx`. Pin the PostgreSQL 17 test image in the fixture configuration.

## Phase 2: Foundational prerequisites

- [X] T003 [P] Define Application-owned top-up, evidence and due-work persistence with customer-scoped admission keys, merchant-scoped references, immutable intent and lease/version protection in `src/Avand.Application.Api/Persistence/ApplicationDbContext.cs` and `src/Avand.Application.Api/Persistence/Migrations/`.
- [X] T004 [P] Define Ledger-owned versioned eligibility, C04 decisions, accounts, journals, projections and operation/action claims in `src/Avand.Ledger.Api/Persistence/LedgerDbContext.cs` and `src/Avand.Ledger.Api/Persistence/Migrations/`. Enforce exact positive IRR entries, unique claims, append-only balanced journals and restricted runtime write privileges.
- [X] T005 [P] Implement persisted Application work claiming, lease expiry, bounded retry scheduling and stale-result protection in `src/Avand.Application.Api/Payments/RecoveryWorker.cs` after T003. Keep network calls outside local transactions and preserve unresolved work on restart.
- [X] T006 [P] Build a deterministic synthetic gateway and fixture clock in `tests/Avand.V01.IntegrationTests/Fixtures/MockGateway.cs`. Return the same session and creation timestamp on replay and support controllable capture, expiry, return, timeout and evidence mismatch.
- [X] T007 Build the two-host HTTP and PostgreSQL fixture in `tests/Avand.V01.IntegrationTests/Fixtures/V01Fixture.cs` after T003, T004 and T006. Apply independent migrations, assign least-privilege credentials, seed two customer identities and accounts, and provide fixture-only eligibility, contention, pre-commit and post-commit fault controls.

## Phase 3: User Story 1 — Initiate a top-up and see the result (P1, MVP)

**Goal** — An eligible customer obtains a session and sees one posted IRR credit and their own authoritative balance.

**Independent test** — Use a seeded eligible customer and deterministic verified payment. Check one balanced credit, exact spendable delta, no gold effect, concurrent duplicate protection, and rejection of unauthorized initiation and reads.

### Tests for User Story 1

- [X] T008 [P] [US1] Write Ledger HTTP contract and PostgreSQL tests for scoped C04/F02, result and balance reads, same-ID replay, changed payload, different-ID same capture, balanced journal and unauthorized writes in `tests/Avand.V01.IntegrationTests/LedgerPostingTests.cs` (AC01–AC03).
- [X] T009 [P] [US1] Write customer-surface tests for stable admission, positive exact IRR, callback verification, concurrent duplicate notices, no gold effect and denied cross-customer reads in `tests/Avand.V01.IntegrationTests/TopUpAcceptanceTests.cs` (AC01–AC04).

### Implementation for User Story 1

- [X] T010 [US1] Implement protected C04 eligibility authorization and scoped result lookup in `src/Avand.Ledger.Api/Controls/TopUpAuthorization.cs`. Use the effective eligibility lock, bind the authorization to the customer and intent, and replay the durable decision.
- [X] T011 [US1] Implement F02 first posting, immutable outcome lookup and balance read in `src/Avand.Ledger.Api/Payments/GatewayCapturePosting.cs`. Claim identity/action, lock eligibility/payment/accounts in deterministic order, post equal receivable and wallet legs, and commit journal, projection and result together. Select the credited bucket from effective eligibility at first posting and preserve it on replay. Verify the Ledger cases before wiring dispatch.
- [X] T012 [US1] Bind authenticated producer scopes and typed internal routes to C04, F02, decision lookup and delegated balance reads in `src/Avand.Ledger.Api/Payments/LedgerPaymentEndpoints.cs` and `src/Avand.Ledger.Api/Program.cs`. Reject unsupported types and unauthorized lookup without financial effects.
- [X] T013 [US1] Implement the typed C04/F02 and readback client in `src/Avand.Application.Api/Payments/LedgerPaymentsClient.cs`. Preserve operation and command identities and distinguish unknown transport outcomes from definitive decisions.
- [X] T014 [US1] Implement durable customer admission and the session workflow in `src/Avand.Application.Api/Payments/TopUpAdmission.cs`. Derive ownership from the authenticated principal, store the canonical acknowledgement and C04 identity, and create the session only after durable Applied. Replay C04 and session creation under their original references.
- [X] T015 [US1] Implement mock session and verify transport in `src/Avand.Application.Api/Payments/MockGatewayAdapter.cs` and callback intake and evidence validation in `src/Avand.Application.Api/Payments/GatewayEvidenceWorkflow.cs`. Authenticate mock notices, coalesce work, bind definitive capture to merchant, payment ID, unique capture ref and exact amount, and commit evidence plus immutable F02 intent together.
- [X] T016 [US1] Implement F02 dispatch and confirmed result application in `src/Avand.Application.Api/Payments/LedgerDelivery.cs`. Write successful status only after Posted and use the stored credited bucket.
- [X] T017 [US1] Expose customer initiation/status/balance and mock notification routes in `src/Avand.Application.Api/Payments/TopUpEndpoints.cs` and wire distinct authenticated customer and mock-provider identities in `src/Avand.Application.Api/Program.cs`. Keep status reads side-effect free and resolve the spendable balance from Ledger.

## Phase 4: User Story 2 — Resolve missing or late notification (P1)

**Goal** — Inquiry works without a callback, with distinct outcomes for capture, definitive rejection, unknown evidence and issuer return.

**Independent test** — Suppress callbacks or deliver a late notice. Drive authoritative and ambiguous gateway responses separately. Control time to check just before, at and after 15:00.

### Tests for User Story 2

- [X] T018 [P] [US2] Write missing-callback, late notification, definitive expiry, incomplete/mismatched evidence and issuer-return acceptance tests in `tests/Avand.V01.IntegrationTests/GatewayInquiryTests.cs` (AC05–AC08). Assert wallet, cash and refund effects independently from status.
- [X] T019 [P] [US2] Write mock gateway contract tests for first verify just before, exactly at and just after 15 minutes from session creation in `tests/Avand.V01.IntegrationTests/GatewayBoundaryTests.cs` (AC13). Also exercise definitive inquiry for prior capture without a verification timestamp.

### Implementation for User Story 2

- [X] T020 [US2] Apply the inclusive first-verify window and evidence response contract in `tests/Avand.V01.IntegrationTests/Fixtures/MockGateway.cs` and the demo adapter at `src/Avand.Application.Api/Payments/MockGatewayAdapter.cs`. Preserve previously verified capture for inquiry after expiry.
- [X] T021 [US2] Schedule initial and recurring inquiry without a callback in `src/Avand.Application.Api/Payments/GatewayEvidenceWorkflow.cs`. Accept late notifications and reuse the same capture path. Only a matched definitive rejection changes the result to Failed. Keep timeout and missing or contradictory evidence unresolved when no definitive result is known.
- [X] T022 [US2] Expose distinct provider-pending, definitive failure and issuer-return messages in `src/Avand.Application.Api/Payments/TopUpStatus.cs`. Record issuer return as completed only after matched confirmation and keep this path free of Avand wallet, cash and refund effects.

## Phase 5: User Story 3 — Recover delayed or interrupted Ledger posting (P1)

**Goal** — A verified capture recovers as one financial effect after delay, response loss or rollback, including under a newly effective restriction.

**Independent test** — Separately delay the first posting, drop its response after confirmed commit, and interrupt it before commit. Check durable state from a fresh connection, then replay the same identity. Exercise restriction before first posting and replay after restriction.

### Tests for User Story 3

- [X] T023 [P] [US3] Write delayed execution, post-commit response loss with Application restart, and pre-commit interruption tests in `tests/Avand.V01.IntegrationTests/LedgerRecoveryTests.cs` (AC09–AC11). Prove the injection point and check journal, projection, claims and outcome before and after recovery.
- [X] T024 [P] [US3] Write effective-restriction, repeated evidence and prior-Posted replay tests in `tests/Avand.V01.IntegrationTests/RestrictedCreditTests.cs` (AC12). Assert one restricted credit, zero spendable delta, visible restricted result and blocked new admission.

### Implementation for User Story 3

- [X] T025 [US3] Extend durable delivery in `src/Avand.Application.Api/Payments/LedgerDelivery.cs` to recover the same stored decision after timeout, NotFound, process restart or failed local apply. Preserve VerifiedPendingLedger until known, never reverify captured evidence, and record definitive Ledger rejection separately from gateway failure.
- [X] T026 [US3] Wire restricted-credit and unresolved-posting status into `src/Avand.Application.Api/Payments/TopUpStatus.cs`. Show restricted credit only after confirmed Posted and use authoritative balances even if status still awaits confirmation.

## Phase 6: Polish and cross-cutting concerns

- [X] T027 Verify safe audit correlation and actionable recovery diagnostics in `src/Avand.Application.Api/Payments/RecoveryWorker.cs` and `src/Avand.Ledger.Api/Payments/GatewayCapturePosting.cs`. Correlate intent, provider action, operation and journal without exposing secrets or raw card data.
- [X] T028 Run the build and two-host suite from `specs/001-fund-irr-wallet/quickstart.md`, review AC01–AC13 and Constitution gates, and record commands, outcomes, durable observations and unexecuted checks in `harness/evidence/001-fund-irr-wallet.md`.

## Dependencies & Execution Order

- Setup precedes foundations. T001 enables T003 and T004. T002 enables T006. T005 follows T003. T007 follows T003, T004 and T006. All foundation tasks precede story work.
- US1 establishes the shared path. After T008–T009, build T010–T012 in order and verify Ledger tests. Then run T013–T017 in order and verify customer tests.
- US2 and US3 build on US1. Within US2, write T018–T019 first, then implement T020–T022. Within US3, write T023–T024 first, then implement T025–T026. Serialize T026 after T022 because both edit status mapping. Finish with T027, then T028.

## Parallel Examples

- Setup and foundation — T001 and T002 can run together. T003 and T004 can run together. T006 can run alongside those once T002 is ready, and T005 can run alongside unfinished fixture or Ledger work once T003 is ready.
- US1 — Author T008 and T009 together against the shared fixture.
- US2 — Author T018 and T019 together in separate test files.
- US3 — Author T023 and T024 together in separate test files. T025 can proceed alongside US2 implementation after US1 and the US3 tests are in place.

## Implementation Strategy

**MVP** — Deliver US1 as the first independently demonstrable increment through the Application API. **Incremental delivery** — Add US2 inquiry and issuer messaging, then complete US3 delayed posting and restriction checks. Full V01 acceptance requires all three stories and verified AC01–AC13 evidence. Keep other Gold slices and Fund/Card outside this task set.
