# D002 - Treat the entered purchase amount as gold principal, with an additional fee

**Status:** Confirmed
**Design checkpoint:** 2026-11-06

[Decision log](../DECISIONS.md) | [Design](../DESIGN.md) | [Defense index](README.md)

## Decision

The entered rial amount represents gold value, not a fee-inclusive budget. At confirmation, the wallet must cover the final rounded gold value plus the buy fee. Reject the entire buy if it cannot.

## Why this fits V1

The customer explicitly chose principal-based input. An explicit total debit avoids unexpectedly reducing the requested trade to pay the fee.

## Alternatives considered

- Fee-inclusive input: a different customer contract.
- Automatically shrink the trade: changes the approved amount and requires new consent.

## Tradeoffs and limits

A wallet equal to the entered principal is insufficient when the fee is positive. UI must display principal, fee and total before confirmation.

## Likely defense question

**Why not buy a smaller amount when the fee does not fit?**

That would change the customer's intended purchase. V1 returns a clear rejection and a revised quote can be requested explicitly.

## Evidence to prepare

For 1,000,000 IRR principal and a hypothetical 10,000 IRR fee, a 1,000,000 IRR wallet is rejected and a 1,010,000 IRR wallet can pass the funding check.

This is planned verification. No implementation or test result is claimed.

## When to revisit

Revisit if product changes the input contract to a fee-inclusive budget.
