# D005 - Separate a 30-second quote lock from a 30-second feed freshness limit

**Status:** Confirmed
**Design checkpoint:** 2026-11-06

[Decision log](../DECISIONS.md) | [Design](../DESIGN.md) | [Defense index](README.md)

## Decision

Issue a quote only when the latest valid provider price is no more than 30 seconds old under the earlier Avand freshness policy. The company confirmed a distinct 30-second quote validity measured from server issuance. Honor an issued quote until its own expiry. After expiry, show a new quote and require explicit customer confirmation. Provider freshness and client display are separate from server-side expiry.

## Why this fits V1

Provider freshness determines whether a new commitment is safe. Quote expiry defines an existing commitment to the customer.

## Alternatives considered

- Invalidate every quote on each provider tick: defeats the promised price lock.
- Continue quoting indefinitely from the last price: accepts unbounded stale-price exposure.

## Tradeoffs and limits

A quote issued near the freshness limit can be honored when the underlying price is almost 60 seconds old. That bounded exposure is intentional.

## Likely defense question

**Why honor a quote after the feed becomes stale?**

Its lock is an existing, time-bounded commitment. Feed freshness controls new commitments rather than retroactively changing accepted quote terms.

## Evidence to prepare

Use a controllable clock to test expiry boundaries, stale-feed issuance rejection and honoring an unexpired quote during an outage.

This is planned verification. No implementation or test result is claimed.

## When to revisit

Revisit lock duration and exposure when actual provider behavior is known.
