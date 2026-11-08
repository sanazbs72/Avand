# D010 - Display an advisory maximum and enforce capacity again at confirmation

**Status:** Confirmed
**Design checkpoint:** 2026-11-06

[Decision log](../DECISIONS.md) | [Design](../DESIGN.md) | [Defense index](README.md)

## Decision

Show the lowest of platform backing capacity, wallet-affordable gold including fee and the configured order cap converted to gold at the buy price. Treat the result as advisory and recheck all applicable limits at confirmation.

## Why this fits V1

The UI helps customers choose a viable size, while only a protected server-side check can survive concurrent activity.

## Alternatives considered

- UI-only validation: races with other users and devices.
- Reserve capacity on every quote: introduces reservation expiry and abandoned-quote cleanup.

## Tradeoffs and limits

The maximum can change between display and confirmation. V1 rejects an oversized request rather than silently resizing it.

## Likely defense question

**Why can a purchase fail after the UI showed enough capacity?**

Displaying a maximum does not reserve it. Another completed trade may consume it before confirmation.

## Evidence to prepare

Two customers see the same remaining capacity, but their concurrent confirmations cannot overspend it.

This is planned verification. No implementation or test result is claimed.

## When to revisit

Add reservations only if product evidence justifies their extra lifecycle.

## Independent-service implications

The Application UI obtains capacity/balance evidence through Ledger contracts. Display remains advisory. Ledger rechecks actual balances and controls on first execution; a previously Posted operation replays its existing result.
