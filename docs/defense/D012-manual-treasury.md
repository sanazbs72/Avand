# D012 - Replenish liquidity through operations, with no automatic custody sale

**Status:** Confirmed
**Design checkpoint:** 2026-11-06

[Decision log](../DECISIONS.md) | [Design](../DESIGN.md) | [Defense index](README.md)

## Decision

Detect low liquidity, create a TreasuryRequest and expose it to operations. Operations arranges funding or a sale of free platform gold. Increase usable liquidity only after a confirmed financial inflow.

## Why this fits V1

The customer explicitly chose a human-operated treasury process for V1.

## Alternatives considered

- Automatically liquidate platform gold: needs execution, pricing and authorization policies.
- Only reject sales without an operations workflow: leaves the shortage unmanaged.

## Tradeoffs and limits

Operations response time affects sale availability. A threshold should trigger action before liquidity is exhausted; its exact value is not yet set.

## Likely defense question

**Does manual treasury mean staff can edit balances?**

No. Staff records and resolves the operational request; ledger-backed evidence of actual inflow changes financial capacity.

## Evidence to prepare

Closing a request without confirmed inflow does not increase capacity. No automatic custody-sell call follows a low-liquidity alert.

This is planned verification. No implementation or test result is claimed.

## When to revisit

Consider automation after execution contracts, controls and operational behavior are proven.

## Independent-service implications

The Application TreasuryRequest and confirmed financial inflow do not share a database. Submit the confirmed movement using a stable Ledger operation, then record its acknowledgement. Closing the operations request cannot manufacture capacity.
