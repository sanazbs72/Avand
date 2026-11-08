---
name: avand-financial-verification
description: Use when designing, running, or reviewing Avand financial acceptance tests, Ledger invariants, PostgreSQL races, crash recovery checks, or evidence for completed financial work.
---

# Avand Financial Verification

Tie every claimed result to an approved requirement, the mechanism that can fail, and evidence from the environment where that mechanism runs.

## Choose the check

Start with the active Spec Kit feature and the [Constitution](../../../.specify/memory/constitution.md). Use [Financial Operations](../../../docs/FINANCIAL_OPERATIONS.md) section 14 and the operation's contract to identify relevant failure cases. Trace the original acceptance scenario to its current form in [Design](../../../docs/DESIGN.md). Keep deliberate adaptations visible. Use [Harness evidence guidance](../../../docs/SDD_HARNESS.md) to record what ran. Treat draft defaults as labeled assumptions until the feature approves them.

| Claim | Check at the boundary that can disprove it |
| --- | --- |
| Exact fees, units and rounding | Test boundary inputs, including one unit below, at and above applicable thresholds. Derive expected values independently from the contract. |
| Correct accounting | Check approved posting legs, asset, account owner, normal side, and exact debit-credit equality per asset. Reconcile durable projections to journal entries for the affected accounts. |
| Database atomicity and write protection | Use real PostgreSQL with actual migrations, constraints and runtime privileges. Interrupt posting before commit and inspect journal, projection, holds, deduplication and result from a fresh connection. Try an unauthorized direct write when testing enforcement. |
| Idempotency and business deduplication | Exercise same key and payload, changed payload under the same key, and a different key for the same protected business or provider action. Verify durable effects and stable replay under the applicable identity. |
| Concurrent funding, backing, liquidity or hold guards | Use separate sessions and requests. Set preconditions so each request can succeed alone but only the allowed combination can commit. Coordinate contention at the guarded boundary and assert legal outcomes and resulting durable state. |
| Application and provider recovery | Use both logical services and their durable stores. Inject a failure at the intended boundary and observe replay, restart or forward repair with the original identity. Use controlled provider behavior for callbacks and ambiguous outcomes. |

Static posting fixtures support arithmetic and journal design. Unit tests support domain behavior. Report PostgreSQL and two-service runtime evidence separately.

## Make the scenario discriminate

- For the same-key public buy burst, assert one durable order, identical canonical acknowledgements, then one eventual Ledger result and one economic effect. Inspect the fee, journal, projection and follow-up intent as applicable. A matching set of HTTP 202 responses alone proves acknowledgement behavior.
- For wallet overspend, use distinct public keys, OperationIds and eligible quotes for the same wallet. Fund enough for either buy including fees but not both. Keep backing, eligibility and other guards permissive. A rejection due to quote reuse, expiry or another guard leaves wallet contention unverified.
- Synchronize the competing work at a relevant boundary using a barrier, controlled lock contention, or equivalent observable coordination. Use independent sessions or service requests. Task.WhenAll alone gives no evidence that the critical paths overlapped. Assert the failure reason, all affected durable state and allowed winner-independent outcomes.
- Control time explicitly for expiry versus confirm. Test a first decision at the boundary separately from a replay of an already decided operation. Use the approved clock and avoid sleep-based timing assumptions.
- For a lost response, verify that the first Ledger transaction committed before suppressing its reply. For rollback, inject before commit. For failed Application apply, observe the durable Ledger result, then restart or retry the local application. Check stable identity, one financial effect and one required follow-up effect.
- For top-up, compare a late callback with definitive verify expiry, an ambiguous provider outcome, and delayed delivery of already verified capture. Assert Failed with no wallet posting or Avand refund dispatch, unresolved status, and one recovered wallet credit respectively. Read back durable state and test duplicate evidence.
- For withdrawals, exercise the ordinary ceiling and one unit above it. Verify ordinary routing at the ceiling and Finance approval before manual payment above it. Assert that a national-ID/Sheba mismatch cannot authorize reserve or dispatch. Read limits and gateway timing from the [company clarifications](../../../docs/COMPANY_CLARIFICATIONS.md) and active contract.
- For Gold orders, test both sides of the paired price, side-specific fee calculation, and the configured lower and upper order bounds using the approved order-value basis.

## Report only observed evidence

For each requirement or scenario record the feature/task ID, original-to-adapted mapping when applicable, source revision or patch identity, verification layer, command and exit code, observed result, and any unverified branch. Link concise logs where available and exclude sensitive payloads. Use Pass, Fail, NotRun or Blocked per check. A missing test, skipped filter or unavailable dependency cannot pass. If work is design-only, label the test cases as proposed and runtime evidence as NotRun. Update the original feature task with its actual status.
