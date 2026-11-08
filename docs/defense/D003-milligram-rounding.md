# D003 - Round traded gold down to whole milligrams and charge for the actual quantity

**Status:** Confirmed
**Design checkpoint:** 2026-11-06

[Decision log](../DECISIONS.md) | [Design](../DESIGN.md) | [Defense index](README.md)

## Decision

For rial-based buys, round the resulting weight down to a whole milligram. Recalculate principal and fee for that actual quantity. Leave the unused rial remainder in the wallet.

## Why this fits V1

Whole-milligram accounting avoids fractional gold balances, while charging for the delivered quantity preserves the customer contract.

## Alternatives considered

- Round weight up: can exceed the requested budget or funding capacity.
- Charge the original amount after rounding weight down: charges for undelivered gold.

## Tradeoffs and limits

A small part of the entered principal can remain unspent. Fractional-rial principal rounding still needs an explicit policy.

## Likely defense question

**Where does the rounding remainder go?**

It remains in the customer's wallet. It is not silently turned into platform revenue or extra fee.

## Evidence to prepare

Boundary tests around a milligram ensure no excess weight is allocated and no unused principal is debited. Minimum-order eligibility is checked against the final trade value once that policy is finalized.

This is planned verification. No implementation or test result is claimed.

## When to revisit

Revisit if custodian precision or the minimum tradable unit changes.
