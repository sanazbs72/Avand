# D015 - Keep financial effects atomic inside Ledger and recover Application status separately

**Status:** Financial invariant retained; earlier cross-module transaction superseded by D026-D027
**Design checkpoint:** 2026-11-06

[Decision log](../DECISIONS.md) | [Design](../DESIGN.md) | [Defense index](README.md)

## Decision

In one Ledger transaction, protect operation identity, financial eligibility, balances and shared controls; post all financial legs and persist the terminal result. Application separately commits intent before the call and result application afterward.

## Why this fits V1

Financial correctness is kept within one authoritative database even though the deployment is split. A lost response changes what Application knows, not whether the journal committed.

## Alternatives considered

- Shared Application/Ledger database transaction: conflicts with the selected service/data ownership boundary.
- Remote debit and credit steps: creates partial financial states avoidable inside Ledger.
- Compensate after a Application status failure: reverses a valid financial result for an operational persistence problem.

## Tradeoffs and limits

The earlier proposal included quote/order/idempotency and ledger entries in one transaction. That is superseded. Application claims the quote and saves intent first; Ledger validates the immutable financial authorization, quote expiry and single use at acceptance. Application status can lag and requires durable recovery.

## Likely defense question

**What remains atomic after the split?**

All financial legs, fee, balance/control changes and durable operation result inside Ledger. Application order state is not part of that commit; query/retry and idempotent result application bring it into agreement.

## Evidence to prepare

Race buys and sales against the same balances and shared budgets. Inject failure before Ledger commit, response loss after commit and failure applying Application status. Verify no partial journal, no duplicate posting and eventual correct status.

This is planned verification. No implementation or test result is claimed.

## When to revisit

Finalize local lock ordering and policy arithmetic. Introduce a richer workflow only when future products create multiple independent business commitments.
