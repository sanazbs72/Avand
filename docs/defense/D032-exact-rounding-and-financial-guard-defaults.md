# D032 - Make numeric and capacity defaults explicit for review

**Status:** Author-proposed defaults; prior confirmed policies retained
**Design checkpoint:** 2026-11-06

[Decision log](../DECISIONS.md) | [Design](../DESIGN.md) | [Chart of accounts](../CHART_OF_ACCOUNTS.md) | [Financial operations](../FINANCIAL_OPERATIONS.md) | [Defense index](README.md)

## Decision

Specify integer IRR-per-gram pricing, whole-mg quantity, buy-principal ceiling, sell-principal floor, fee ceiling with ppm rates, exact cash obligations and conservative outgoing-gold capacity. The company confirmed both order limits and both fee rates in D036. Applying the limits to final pre-fee value after conversion remains the proposed calculation rule.

## Why this fits V1

Affordability, minimum-order checks and idempotent financial posting cannot be implemented consistently while principal rounding and cash/headroom definitions remain unspecified.

## Alternatives considered

- Leave rounding to each caller: produces inconsistent quotes and postings.
- Subtract every payable and reservation twice: falsely removes capacity.
- Ignore reservations or treat expected receipts as cash: can double allocate assets.

## Tradeoffs and limits

The healthy-mode 1% cap, conservative outgoing reservation denominator, restricted incoming-funds handling, demo buffer/threshold and correction controls remain proposals. Withdrawal limits and manual routing are confirmed by D036. Exact manual approval and hold timing still require a contract.

## Likely defense question

**Does the extra 1% apply separately to each user or outage?**

No. It bounds aggregate exposure. Outgoing reservations reduce the effective future custody available for new trades; unconfirmed purchases and suspense do not increase it. The confirmed no-reservation example remains unchanged.

## Evidence to prepare

Check whole-unit arithmetic and min-order boundaries, no-double-count liquidity examples, and capacity before/after outgoing reservations. Verify financial approval of new defaults before claiming implementation readiness.

This is planned verification. Static accounting fixtures are not executable application tests.

## When to revisit

Adjust policy values from operations evidence and explicit approval. Do not change historical quote amounts or results on retry.
