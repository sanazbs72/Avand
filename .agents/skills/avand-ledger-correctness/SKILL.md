---
name: avand-ledger-correctness
description: Use when designing, implementing, or reviewing Avand Ledger postings, balance projections, fees, financial holds, spendable balances, custody backing, treasury liquidity, or journal corrections.
---

# Avand Ledger Correctness

Trace each financial operation from its approved contract through its posting, transaction and observable result. Identify the invariants at risk before changing the write path.

## Locate the applicable contract

Start with the active feature specification and the [Constitution](../../../.specify/memory/constitution.md). Open the relevant sections of [Financial Operations](../../../docs/FINANCIAL_OPERATIONS.md) for the typed operation, eligibility, guards and persistence. Use the [Chart of Accounts](../../../docs/CHART_OF_ACCOUNTS.md) to verify asset, owner and normal side. Use [Posting Examples](../../../docs/LEDGER_POSTING_EXAMPLES.md) to check the expected legs and [Ledger Service](../../../docs/LEDGER_SERVICE.md) for the authoritative service boundary.

Check the status of each source. Draft formulas, limits, lock orders and SQL designs need approval in the applicable feature contract before implementation. If a needed source is missing or conflicts, name the affected decision and continue with the checks supported by available evidence. Treat examples as test inputs and expected results.

## Review the financial write

1. **Identify the posting.** Name the typed operation, eligible actor, customer or platform owner, evidence, stable operation ID and business/provider action identity where applicable. Identify which resources it touches and which preconditions apply.
2. **Reconcile the legs.** Derive each debit and credit from the operation and account map. Balance every journal separately per asset. Validate positive line amounts, account asset and ownership, and protected final balances. Use checked exact arithmetic, including intermediate products and sums, with the approved rounding for each amount. Keep fees explicit. A buy must cover delivered principal plus fee.
3. **Protect the decision.** Evaluate spendable funds, holds, effective eligibility, custody backing and cash liquidity under the same concurrency protection as the posting. Use the approved lock order or equivalent approved database mechanism across competing writers. A cached read, in-process lock or an earlier check followed by a transaction around writes cannot authorize concurrent spending.
4. **Commit one financial outcome.** One Ledger-local transaction commits all legs, fees, synchronous spendable projections, affected holds and controls, deduplication claims and the durable replayable operation result. Roll them back together on technical failure. Reconstructing a response later from journal or current state is not a substitute for the committed operation result. Exact retries return the stored outcome. A changed economic payload under the same identity conflicts. An ambiguous commit needs lookup or retry with the original identity. Keep network calls outside the database transaction.
5. **Preserve auditability.** Keep posted journals append-only and spendable projections reconstructable from entries. Route financial writes through the controlled posting boundary, with database protections against bypass and a commit-time check for aggregate balance. An individual row CHECK cannot enforce per-journal equality. Correct a posting through an authorized linked reversal and any allowed replacement. Check the reversal's own guards before the replacement.

## Distinguish financial resources

- Separate customer entitlements, verified custody, confirmed usable cash, receivables, and suspense. Pending procurement cannot increase backing. A treasury receivable or suspense balance cannot fund a sale. Count each payable or reservation once and preserve exposure for potentially dispatched holds until supported final evidence.
- Share the same Ledger financial capacity across products and concurrent writers. Apply the approved backing, liquidity and eligibility rules inside the posting decision. Recheck how an outgoing custody reservation affects capacity for a simultaneous buy.
- A restriction blocks applicable new instructions. Preserve verified financial facts from earlier work and apply the approved restricted-funds policy.

## Collect evidence for the affected path

For each relevant invariant, name the failure a test would detect and the observable result. Use approved posting fixtures for account direction and arithmetic. Use real PostgreSQL with independent sessions for concurrency, constraints and transactional rollback. Inspect the actual transaction and write permissions in review. An in-memory database or fixture cannot establish PostgreSQL behavior.

For example, review a buy against an available wallet that can cover only one of two concurrent buys including fees. Exercise both requests with different operation IDs through the financial boundary. Check that only one posts and that its journal balances per asset, the projection matches the entries, and its result replays without another posting. In a separate failure case, interrupt the transaction after writing the journal and verify that no partial journal, projection, hold, deduplication claim or success result survives. Use actual failure injection or report this check as unrun.

For a review, report the operation, violated invariant, exact location, consequence and evidence needed to close the finding. Distinguish confirmed defects, unresolved contract choices and checks that did not run.
