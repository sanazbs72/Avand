# D006 - Use an append-only double-entry ledger as the financial source of truth

**Status:** Confirmed principle; detailed D030 account catalog proposed for review
**Design checkpoint:** 2026-11-06

[Decision log](../DECISIONS.md) | [Design](../DESIGN.md) | [Defense index](README.md)

## Decision

Record financial changes as immutable journal entries. Each journal must balance debits and credits independently for each asset. Derive balances from those entries. Correct mistakes with linked new entries rather than edits or deletions.

## Why this fits V1

A mutable balance alone cannot explain how an asset amount arose, detect partial posting, or support financial reconstruction.

## Alternatives considered

- Store only current balances: insufficient history and contrary to the retained ledger requirement.
- Edit old entries during corrections: destroys the original financial history.

## Tradeoffs and limits

Posting, corrections and account ownership need deliberate design. Balanced journals alone do not prove correct prices, actual cash or physical backing.

## Likely defense question

**Does double-entry guarantee the gold exists?**

No. It proves a bookkeeping invariant. Custodian confirmation, backing controls and reconciliation independently address physical assets.

## Evidence to prepare

Every journal balances by asset. Recomputed balances match stored projections. Duplicate business operations cannot produce a second journal.

This is planned verification. No implementation or test result is claimed.

## When to revisit

Retain this invariant; refine the chart of accounts with finance before production.

## Detailed contract

CHART_OF_ACCOUNTS now defines the complete draft baseline taxonomy, normal sides, owner scopes, balance bounds and physical backing/cash meaning. FINANCIAL_OPERATIONS defines typed posting and correction templates; the catalog is proposed, not absent.
