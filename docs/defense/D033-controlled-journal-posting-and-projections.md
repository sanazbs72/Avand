# D033 - Restrict journal writes and verify projections inside the Ledger transaction

**Status:** Database enforcement and lock strategy proposed
**Design checkpoint:** 2026-11-06

[Decision log](../DECISIONS.md) | [Design](../DESIGN.md) | [Chart of accounts](../CHART_OF_ACCOUNTS.md) | [Financial operations](../FINANCIAL_OPERATIONS.md) | [Defense index](README.md)

## Decision

Use a controlled post_journal boundary with restricted journal/projection DML, complete-journal per-asset validation, aggregate account updates and a deferred database validation. Couple journals, operation outcomes, evidence claims and controls in one Ledger transaction.

## Why this fits V1

A rebuildable balance projection needs one write path and a concrete enforcement strategy. A row CHECK alone cannot prove a multirow journal sum or protect earlier funding reads.

## Alternatives considered

- Only call SaveChanges and trust conventions: misses protected read-check-write and cross-row invariants.
- Permit independent projection updates: creates drift paths.
- Build a generic event-sourcing platform first: adds infrastructure outside the baseline requirement.

## Tradeoffs and limits

The stored function/triggers are proposed, not implemented. V1 serializes short financial decisions on a shared control row, then locks related state/accounts deterministically. Measure contention; do not promise throughput without a test.

## Likely defense question

**Can the journal balance while its projection is wrong?**

Yes, unless the write path couples both. The proposed boundary derives balance changes from entry deltas once, restricts other writers and verifies invariants at commit. Recovery replays the stored operation result rather than posting again.

## Evidence to prepare

Attempt direct invalid writes, unbalanced journals, duplicate posting calls and failure between entries/projection/result. Compare exact reconstructed balances after concurrent operations using real PostgreSQL.

This is planned verification. Static accounting fixtures are not executable application tests.

## When to revisit

Refine SQL/migrations and guarded-resource partitioning from actual test evidence while keeping the financial API invariant.
