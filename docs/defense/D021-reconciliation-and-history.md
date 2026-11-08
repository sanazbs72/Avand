# D021 - Retain bidirectional reconciliation and traceable operation history

**Status:** Retained requirement; detailed workflow remains proposed
**Design checkpoint:** 2026-11-06

[Decision log](../DECISIONS.md) | [Design](../DESIGN.md) | [Defense index](README.md)

## Decision

Compare external records with ledger-linked operations in both directions. Preserve event history and correlate payments, trades, journals and custody work. Distinguish report time, transaction time and settlement time.

## Why this fits V1

Balanced books can still omit or duplicate external economic activity. Support needs an explainable trail.

## Alternatives considered

- Only compare totals: opposite errors can cancel.
- Only search external records missing internally: misses the reverse discrepancy.

## Tradeoffs and limits

The company confirmed a daily custody CSV and daily IRR settlement on business days. Reconcile physical gold and cash separately. DESIGN section 13.1 proposes the mock CSV schema. Cutoffs, tolerances, escalation ownership and closure evidence still need specification.

## Likely defense question

**Why reconcile if every transaction balances?**

Balance proves internal bookkeeping consistency, not agreement with the gateway, bank or physical custodian.

## Evidence to prepare

A gateway-only transaction and a ledger-only transaction both appear in the report. Unknown external operations remain visible.

This is planned verification. No implementation or test result is claimed.

## When to revisit

Finalize formats, cutoffs and remediation rules when external mock contracts are specified.

## Independent-service implications

Reconciliation also compares Application state with Ledger operation/journal evidence through authorized APIs. Record as-of times and references; there is no cross-database atomic snapshot or direct table join. A Posted outcome with a stale Application status is a recovery discrepancy, not missing money.
