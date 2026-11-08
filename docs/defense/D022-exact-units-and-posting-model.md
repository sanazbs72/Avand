# D022 - Use exact asset units and a small journal schema

**Status:** Working proposal
**Design checkpoint:** 2026-11-06

[Decision log](../DECISIONS.md) | [Design](../DESIGN.md) | [Defense index](README.md)

## Decision

Represent IRR and GOLD_MG amounts as C# long and PostgreSQL bigint, with decimal/numeric for rates and intermediate arithmetic. Use LedgerAccounts, JournalTransactions, JournalEntries and AccountBalances as the initial schema vocabulary.

## Why this fits V1

Integer asset units and explicit journal grouping make invariants and rounding inspectable without a separate event-sourcing platform.

## Alternatives considered

- Binary floating point for financial amounts: introduces avoidable representation error.
- Full event sourcing for every domain object: adds infrastructure not required by an immutable financial journal.

## Tradeoffs and limits

Use checked range handling and sufficiently wide database aggregates. FINANCIAL_OPERATIONS and CHART_OF_ACCOUNTS now specify draft principal rounding, account classifications and controlled posting under D030-D033. Their approval and actual implementation remain work, not missing contract definitions.

## Likely defense question

**Is an append-only ledger the same as event sourcing the whole application?**

No. Financial entries are immutable, while orders and other operational records may use ordinary state persistence plus an audit history.

## Evidence to prepare

Boundary tests cover fee rounding, milligram conversion, overflow handling and balanced journals per asset.

This is planned verification. No implementation or test result is claimed.

## When to revisit

Change numeric precision or schema only with explicit unit and migration policies.

## Independent-service implications

The proposed Ledger schema additionally needs FinancialOperations and authoritative financial-control/effective-eligibility records. Application orders, quote claims and dispatch intent are in Application's database. Neither service imports the other's persistence model.
