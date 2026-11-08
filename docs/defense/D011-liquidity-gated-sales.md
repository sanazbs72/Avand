# D011 - Accept a gold sale only when its net proceeds can be immediately withdrawable

**Status:** Confirmed
**Design checkpoint:** 2026-11-06

[Decision log](../DECISIONS.md) | [Design](../DESIGN.md) | [Defense index](README.md)

## Decision

Credit sale proceeds net of fee to the spendable rial wallet immediately. Reject a new sale when available platform liquidity cannot cover that obligation. Do not accept it into a delayed-cash state.

## Why this fits V1

The chosen product promises immediate wallet availability and withdrawal eligibility, rather than a pending redemption.

## Alternatives considered

- Accept and block proceeds until cash arrives: a different product state.
- Accept regardless of liquidity: creates obligations the platform may not pay on demand.

## Tradeoffs and limits

Sales can be temporarily unavailable. Liquidity is confirmed usable cash minus distinct existing obligations and earmarks, not just the bank account balance.

## Likely defense question

**Why not count a planned custody sale as cash?**

The cash has not arrived and may be delayed or fail. A plan cannot satisfy an immediately withdrawable customer claim.

## Evidence to prepare

Concurrent sales share one liquidity budget. Insufficient-liquidity rejection changes neither customer gold nor wallet.

This is planned verification. No implementation or test result is claimed.

## When to revisit

Revisit only if product explicitly permits pending sale proceeds.

## Independent-service implications

Ledger owns the authoritative liquidity budget and consumes it in the same transaction as the sale. Application Treasury manages replenishment, not an independent spendable-cash counter. Exact obligation/hold arithmetic remains open.
