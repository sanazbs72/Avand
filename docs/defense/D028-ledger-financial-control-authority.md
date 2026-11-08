# D028 - Enforce spending, backing, liquidity and effective financial restrictions inside Ledger

**Status:** Working authority design preserving confirmed policies
**Design checkpoint:** 2026-11-06

[Decision log](../DECISIONS.md) | [Design](../DESIGN.md) | [Ledger service](../LEDGER_SERVICE.md) | [Defense index](README.md)

## Decision

Place the authoritative state used to accept a financial operation inside Ledger: balances, gold-backing facts/counters, liquidity commitments and effective financial eligibility. Application modules choose policy and verify external facts, then submit authorized versioned/deduplicated updates.

## Why this fits V1

A Application-side capacity check followed by an unguarded remote posting can race. The same shared financial boundary must protect Gold and future Fund/Card spending.

## Alternatives considered

- Check policy against mutable Application copies: cannot atomically authorize the subsequent remote posting.
- Allow each product an independent available balance: permits multiple products to spend the same funds.
- Put provider calls inside Ledger locks: extends financial locks across external failures.

## Tradeoffs and limits

Ledger includes financial authorization controls as well as bookkeeping. This is intentional and should remain narrower than product pricing or provider integration. CHART_OF_ACCOUNTS and FINANCIAL_OPERATIONS now define exact draft liquidity/reservation arithmetic and healthy-mode defaults. The new choices require review.

## Likely defense question

**How can customer suspension be immediate if profile and Ledger live in different databases?**

Define effective financial restriction at the Ledger commit of a monotonically versioned policy update. Application blocks new local initiation as soon as requested and does not report global effectiveness until Ledger acknowledges it. Activation remains disabled until acknowledged. A concurrent trade and restriction have one Ledger ordering.

Application commits desired state, version and durable control-command intent together. Retry/read-back recovers dispatch failure or a lost acknowledgement. Ledger commits the control change and command outcome together. An old acknowledgement cannot mark a newer desired version effective. Other administrative financial-control updates need the same recovery discipline.

## Evidence to prepare

Race a trade against restriction and shared-capacity changes. Verify out-of-order policy updates cannot reactivate an older state. Show that verified incoming facts post once and expected cash/in-flight gold do not create spendable capacity.

Crash before control dispatch and lose the acknowledgement after Ledger applies a restriction. Verify recovery and version-matched effectiveness without a Saga framework.

This is planned verification. No implementation or test result is claimed.

## When to revisit

Review the detailed proposed outgoing holds, cash obligations, restricted-funds handling and policy defaults. Map actual custody evidence before production activation. Separate policy authoring from atomic enforcement without weakening the latter.
