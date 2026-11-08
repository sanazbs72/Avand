# D007 - Maintain balances synchronously in the ledger transaction

**Status:** Working proposal supporting the agreed ledger model
**Design checkpoint:** 2026-11-06

[Decision log](../DECISIONS.md) | [Design](../DESIGN.md) | [Defense index](README.md)

## Decision

Keep AccountBalances in PostgreSQL and update it in the same transaction as the journal entries. Use the protected current balance for funding checks and rebuild it from entries when needed.

## Why this fits V1

Fast reads do not require repeatedly summing a full account history. Synchronous updates avoid stale spendable balances.

## Alternatives considered

- Sum all entries for each read: simple initially but grows with account history.
- Asynchronous spendable-balance projection: requires another authoritative concurrency mechanism.

## Tradeoffs and limits

Every posting updates balance rows, creating intentional contention on shared accounts. Reports may later use asynchronous projections.

## Likely defense question

**Is this a second source of truth?**

No. It is a rebuildable projection whose updates are coupled to the ledger transaction, with no independent balance-edit path.

## Evidence to prepare

Inject failures before commit and verify that journal and balance changes both roll back. Recompute balances after concurrent operations.

This is planned verification. No implementation or test result is claimed.

## When to revisit

Optimize reads or split reporting only after measured contention or query cost.

## Independent-service implications

Under D026-D027, entries and balances share the Ledger database transaction. Application order status lives in the other database and may lag. No Application-side balance projection is authoritative for spending.
