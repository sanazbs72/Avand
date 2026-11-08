# D009 - Bound uncovered gold by 1% of total confirmed custody

**Status:** Confirmed outage policy; D032 healthy-mode/reservation defaults proposed
**Design checkpoint:** 2026-11-06

[Decision log](../DECISIONS.md) | [Design](../DESIGN.md) | [Defense index](README.md)

## Decision

Let C be total confirmed pooled custody in mg, U total customer gold claims, and L = floor(C / 100). During custodian unavailability, accept a buy of q only when max(0, U + q - C) <= L. The 1% denominator is total confirmed custody, not free platform stock.

## Why this fits V1

The customer explicitly accepted this bounded hybrid-inventory policy and the example using 1,000 g custody and 995 g customer claims.

## Alternatives considered

- Require full backing before every trade: lower exposure but lower availability.
- Unbounded uncovered trades: no finite exposure control.

## Tradeoffs and limits

This deliberately permits a short physical position. It is a business assumption, not a claim of regulatory approval. In-flight procurement does not count as confirmed custody.

## Likely defense question

**Is the allowance per customer or per outage?**

Neither. It bounds the platform's aggregate current shortfall, including any shortfall that already exists. An outage does not reset a counter.

## Evidence to prepare

At C = 1,000 g and U = 995 g, capacity is 15 g. Concurrent buys cannot jointly exceed it. Zero custody produces zero uncovered allowance.

This is planned verification. No implementation or test result is claimed.

## When to revisit

Confirm normal-operation policy, stale custody evidence and production authorization before launch.

## Independent-service implications

D028 places C, U and the atomic backing guard inside Ledger. Custody supplies deduplicated verified facts. A Application-side precheck cannot replace the protected Ledger check. CHART_OF_ACCOUNTS now supplies concrete outgoing-hold and healthy-mode defaults under D032; these new choices require review, not independent implementation guesses.
