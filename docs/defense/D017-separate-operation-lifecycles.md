# D017 - Keep customer trades, top-ups, custody operations and treasury requests separate

**Status:** Confirmed separation; exact state names proposed
**Design checkpoint:** 2026-11-06

[Decision log](../DECISIONS.md) | [Design](../DESIGN.md) | [Defense index](README.md)

## Decision

A completed customer trade is independent of its platform procurement state. A top-up is independent of a buy. Custody and payment ambiguity remain visible to support. A TreasuryRequest records operations work without changing assets by itself.

## Why this fits V1

One overloaded order state cannot express completed customer ownership and unresolved external work accurately.

## Alternatives considered

- One state machine for payment, trade and custody: couples unrelated outcomes.
- Hide unknown operations: prevents support and reconciliation from resolving them.

## Tradeoffs and limits

Support needs correlation across several records. Identifiers and event history must be designed deliberately.

## Likely defense question

**What does the customer see if procurement is pending?**

The accepted customer trade is Completed. Procurement is the platform's separate responsibility, bounded by backing controls.

## Evidence to prepare

Support can trace each order to its journal and procurement work, and each top-up to verification and posting attempts.

This is planned verification. No implementation or test result is claimed.

## When to revisit

Change customer-facing states only if finality or availability policy changes.

## Independent-service implications

Add FinancialOutcomeUnknown for unresolved Application knowledge and VerifiedPendingLedger for known payment success awaiting posting. Ledger's durable outcome is authoritative; Application state can lag. Correlate both database records by permanent operation identity.
