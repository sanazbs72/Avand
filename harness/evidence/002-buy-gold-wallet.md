# V02 wallet-funded gold purchase verification evidence

**Date** — 2026-11-08  
**Feature** — `002-buy-gold-wallet`  
**Checkpoint** — T001–T028 complete

## Pre-implementation review

The requirements checklist passed 16/16. Analyze found no blocking discrepancy across the Constitution, approved specification, plan and task order. The eight functional requirements and 16 acceptance scenarios have task coverage. The V01 wallet and Ledger-owned financial decisions remain the implementation boundaries.

| Coverage | Tasks |
| --- | --- |
| FR-01–03 / AC-01–06, customer ownership and exact quote, wallet and posting behavior | T004, T007–T016 |
| FR-04–05 / AC-07–12, quote time, wallet contention and shared backing | T011, T014, T017–T020 |
| FR-06–08 / AC-13–16, single posting, recovery and incremental procurement | T011, T015–T016, T021–T025 |
| Constitution XI–XIII, real storage, least privilege and regression evidence | T001, T003, T006–T007, T025–T028 |

## T001 observed checks

| Command | Exit | Result |
| --- | --- | --- |
| `dotnet build Avand.slnx --ignore-failed-sources` | 1 | Failed without compiler diagnostics. MSBuild reported zero warnings and zero errors. |
| `dotnet build Avand.slnx --ignore-failed-sources --no-restore --disable-build-servers -m:1` | 0 | Passed using the local restore cache. Built both hosts and the V01 integration test assembly with zero warnings and errors. |
| `dotnet test tests/Avand.V01.IntegrationTests/Avand.V01.IntegrationTests.csproj --no-build --filter 'FullyQualifiedName!~V02' --verbosity minimal` | 1 | Sandboxed run aborted before test execution. VSTest did not connect to the test host within 90 seconds. |
| Same V01 test command outside the sandbox | 1 | 15 selected, 4 passed, 11 reported failed during fixture initialization. Testcontainers received HTTP 500 from Docker before the PostgreSQL fixture could start. The 11 PostgreSQL-backed results are Blocked by infrastructure. |
| `docker version --format '{{.Server.Version}}'` and `docker --context default version --format '{{.Server.Version}}'`, outside the sandbox | 1 each | Docker Engine responded HTTP 500 on both the Desktop Linux and default named pipes. Docker Desktop itself reported running. |
| `git diff --check` | 0 | No whitespace errors in the tracked diff. Git reported existing line-ending notices on the two project files. |

`AVAND_V01_TEST_POSTGRES` was unset and the default local PostgreSQL port was closed. The existing Ubuntu WSL distribution had no installed PostgreSQL binaries or cached server package. Its local package metadata showed PostgreSQL 14 as the default, rather than the fixture's pinned PostgreSQL 17.6 baseline.

**Resume** — Docker Engine returned version 27.5.1. The same V01 suite outside the sandbox exited 0 with 15 passed, 0 failed and 0 skipped against the pinned PostgreSQL fixture. T001 passed.

## T002 fixture verification

The cached solution build exited 0 with no warnings or errors. `dotnet test tests/Avand.V01.IntegrationTests/Avand.V01.IntegrationTests.csproj --no-build --filter FullyQualifiedName~V02FixtureTests --verbosity minimal` exited 0 with one test passed against both real HTTP hosts and PostgreSQL 17.6. The V02 fixture funded a spendable wallet through a 1,500,000 IRR V01 top-up.

## Foundation pre-change check

`dotnet test tests/Avand.V01.IntegrationTests/Avand.V01.IntegrationTests.csproj --no-build --filter 'FullyQualifiedName~V02QuoteMathTests|FullyQualifiedName~V02LedgerStorageTests' --verbosity minimal` selected 12 tests and exited 1. All nine pure arithmetic cases passed (T004). The three T003 storage tests were blocked before the assertions by Docker HTTP 500 in Testcontainers fixture initialization. This is not a demonstrated storage regression.

## T003–T027 implementation verification

**Date** — 2026-11-08. Real PostgreSQL 17.6 (Testcontainers, Docker 27.5.1), both real HTTP hosts, separate runtime roles and separate Payments and Trading Ledger credentials. All data synthetic.

### Implemented

| Task | Landing point |
| --- | --- |
| T005–T006 | Existing `V02AssetJournal` migration and `V02JournalSql` (Codex). Verified, unchanged except for new runtime grants in the fixtures. |
| T007 | `Payments/LedgerPaymentEndpoints.cs`: one route set, dispatch by authenticated producer and operation type, 403 for a type the producer may not submit, distinct-credential check at startup. |
| T011–T012 | `Trading/BuyGold.cs`, `Trading/TradingFinancialState.cs`, `LedgerLocks.cs`. Lock order: operation identity → backing guard → eligibility → accounts. Stored replay before current guards. Decision time sampled after the locks. Journal, projections, claims and outcome commit in one transaction. |
| T010 | Application migration `V02BuyOrders` (BuyQuotes, BuyOrders, BuyRecoveryWork, ProcurementIntents). Generated with the cached dotnet-ef 10.0.13 tool. |
| T013–T016, T019–T020 | `Trading/LedgerTradingClient.cs`, `PairedPriceSource.cs`, `BuyQuoteService.cs`, `BuyOrderAdmission.cs`, `BuyOrderDelivery.cs`, `BuyEndpoints.cs`, `CustomerAccess.cs`. |
| T023–T024, T026 | `Trading/BuyRecoveryWorker.cs` (leased, version-checked, bounded 0.25–5 s backoff, system clock for leases). Correlated logs: order, operation, journal, reason and increment. No credentials or payloads are logged. |
| T027 | Payments and Trading credentials are distinct and enforced in both hosts. The Trading client has no Payments fallback. Runtime roles have no journal or projection DML (`V02LedgerStorageTests`). Application uses only Ledger HTTP contracts. |

### Test classes and acceptance coverage

| Class | Covers |
| --- | --- |
| `V02QuoteMathTests` | AC-01–03 arithmetic (static oracle) |
| `V02LedgerStorageTests` | T003: V01 upgrade preserved, runtime DML denied, append-only history, mixed-asset imbalance, invalid F04 template legs |
| `V02LedgerScopeTests` | T007: wrong producer/type, inquiry and balance scope |
| `V02CustomerContractTests` | AC-01–03, AC-06: exact terms, configured fee, inclusive final-principal limits, ownership |
| `V02BuyJourneyTests` | AC-04–06: V01-funded purchase, per-asset balance, one rial short, restriction before first decision, unavailable balance (503, never 0) |
| `V02QuoteBoundaryTests` | AC-07–08: price age 30 s inclusive and +1 tick, invalid/future/non-UTC times, decision at expiry −1 µs posts and at expiry rejects, refresh with a new confirmation, blocked decision crossing expiry, quote still valid after feed failure |
| `V02CapacityTests` | AC-09–10, AC-12: wallet race and backing race (normal and outage), with the backing guard held from an independent session until both decisions queue; unavailable evidence; advisory max = min(backing, wallet, cap), below minimum |
| `V02BackingCapacityTests` | AC-11 with the spec figures: 975,000/20,000 → normal 5,000 and outage 14,800; 995,000/0 → normal 5,000 and outage 15,000; +1 mg rejected and at-cap posted |
| `V02IdempotencyTests` | AC-13: 100 identical concurrent confirmations → one receipt, order, decision and journal; key/content conflict; quote reuse; Ledger replay, changed-payload 409, DuplicateBusinessAction and QuoteAlreadyUsed |
| `V02RecoveryTests` | AC-14–15: commit proven before the reply is lost, then restart Application after expiry; first decision at expiry is a durable rejection on replay; unanswered send stays unresolved; Ledger precommit fault leaves no partial effect |
| `V02ProcurementTests` | AC-15–16: covered buy creates no intent; 6,000 mg → 1,000 mg intent after an Application apply fault; next 2,000 mg → 2,000 mg; rejection creates none; replay and restart keep 2 intents totalling 3,000 mg |

**AC-16 note** — The covered 5,000 mg case uses the literal state (980,000 remaining custody, 975,000 mg claims). The 6,000 mg case starts from the same 5,000 mg surplus with 985,000 remaining and 980,000 claims, because claims cannot be reduced in one store.

**Not covered at runtime** — Settling an outflow (reducing C and G together) has no posting template in V02. The single deduction of active commitments is verified through `OutgoingMg` in AC-11.

### T028 observed checks

First full V02 run: 39 selected, 38 passed. `LedgerReplaysTheStoredDecisionAndRejectsConflictingIdentities` failed because the test's "changed" envelope was identical to the original, so the Ledger correctly replayed it. The test now changes the quantity, and the identical rebuild is asserted as a replay.

Second full run (`v02-final.trx`): 53 passed and 1 failed. In the 100-confirmation burst, 2 of 100 identical requests got 409. This was a real admission race: a request missed the key lookup, another request committed in between, and the first then classified the same order as quote reuse. Fixed in `BuyOrderAdmission.cs`, which now always re-resolves the customer/key claim before returning a quote-reuse conflict, as the contract requires.

| Command | Exit | Result |
| --- | --- | --- |
| `dotnet build Avand.slnx --ignore-failed-sources --no-restore` | 0 | 0 warnings, 0 errors |
| `dotnet test tests/Avand.V01.IntegrationTests/Avand.V01.IntegrationTests.csproj --no-build --logger "trx;LogFileName=v02-final2.trx"` | 0 | 54 selected, 54 passed, 0 failed, 0 skipped (6 min 6 s). Selection: 39 V02 tests (the 9 arithmetic cases are static, the rest run against PostgreSQL and both hosts) and 15 V01 regression tests on the evolved schema. |

**Outstanding** — Repeated runs of the 100-confirmation burst to measure stability under load: NotRun. No latency or throughput target is approved, so none was measured.

## Burst HTTP 500 follow-up (AC-13) — 2026-11-08

**Trigger** — In the V03 T007 full run, 1 of 100 identical `POST /api/v1/gold/buy-orders` confirmations in `HundredIdenticalConfirmationsProduceOneOrderAndOneFinancialEffect` returned HTTP 500. It then passed on reruns.

**Root cause** — An unhandled transient PostgreSQL failure in buy admission, caused by connection-slot exhaustion in the test environment.
- The Application and Ledger runtime pools used Npgsql's default `Max Pool Size` of 100 each. Both share one PostgreSQL 17.6 server with `max_connections` 100, 3 of which are reserved for superusers.
- A 100-request burst can open almost 100 Application sessions, and the Ledger and admin pools also hold sessions. A new session is then refused with `53300` (too many connections).
- `BuyOrderAdmission` caught only `DbUpdateException` with a unique violation. The `PostgresException 53300` from the first lookup was unhandled, so ASP.NET returned 500.
- This contradicts the contract (`docs/FINANCIAL_OPERATIONS.md` §4: a transient database failure is 503 or a timeout, and the caller retries with the same identity).
- **Evidence status:** the logs of the original failure were not kept, so 53300 as its cause is inferred from code and configuration. The new regression test reproduces the same `53300` deterministically.

**Fix**
- **Application defect:** `BuyOrderAdmission` and `SellOrderAdmission` now map any transient `NpgsqlException` (`IsTransient`: 53300, connection loss, serialization or deadlock) to 503 `{ code: "OrderAdmissionUnavailable", retryable: true }`. Admission is one atomic commit, so a same-key retry resolves the outcome.
- **Test environment:** `V01Fixture` gives both runtime connection strings `MaxPoolSize = 32`. A burst now queues inside the pool instead of overrunning the shared server. This is the budget a deployment must keep, with pool totals below `max_connections`.
- **Regression:** `V02IdempotencyTests.ExhaustedDatabaseConnectionsReturnRetryableUnavailableAndTheSameKeyLaterAdmits`.
  - It sets the Application role's `CONNECTION LIMIT 0`, ends that role's sessions and clears the pools.
  - It asserts that the confirmation returns 503 retryable with no order row.
  - After restoring the limit, it asserts that the same key returns 202 twice with one order.

| Command | Exit | Result |
| --- | --- | --- |
| `dotnet build Avand.slnx --ignore-failed-sources` | 0 | 0 warnings, 0 errors |
| `dotnet test ... --no-build --filter FullyQualifiedName~V02IdempotencyTests` (`v02-idempotency.trx`) | 0 | 3 passed (the 100-burst, the Ledger replay and the new 53300 regression) on PostgreSQL 17.6 and both hosts. |
| First regression attempt, filling global slots with superuser sessions (`v03-all.trx`) | 1 | The confirmation got 202: pooled sessions that were busy when the pools were cleared freed their slots afterwards. Replaced with the role connection limit, which is deterministic. |

**Outstanding** — Repeated stress runs of the 100-burst under the full suite: NotRun beyond the T027 full run recorded in `003-sell-gold-wallet.md`.
