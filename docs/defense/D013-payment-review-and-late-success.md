# D013 - Earlier unresolved top-up review policy

**Status:** Historical 20-minute rule superseded by [D036](D036-company-confirmed-v1-rules.md). Verified capture still posts once.
**Design checkpoint:** 2026-11-06

[Decision log](../DECISIONS.md) | [Design](../DESIGN.md) | [Defense index](README.md)

## Decision

The previous design used a 20-minute operations review threshold. The company now requires callbacks at any arrival time and server-to-server verify/inquiry. In the mock, definitive verification expiry after the 15-minute window means Failed and no wallet credit. Ambiguous results still require inquiry.

## Why this fits V1

Provider-confirmed failure and an unconfirmed network outcome need different handling. Evidence of an already verified capture can reach Ledger later and still post once.

## Alternatives considered

- Mark every timeout Failed: can strand a successful debit.
- Credit on callback alone: treats an unverified notification as payment proof.

## Tradeoffs and limits

The issuer handles the unverified card return within the company's stated 72 hours. Avand records the verified gateway status and communicates it. Provider uncertainty can enter PendingReview under an operations policy.

## Likely defense question

**Does this violate the missing-callback test's final-state expectation?**

The controlled success/failure mock must resolve to a final state. Persistent provider uncertainty is explicitly surfaced as PendingReview instead of inventing a financial outcome.

## Evidence to prepare

Test successful inquiry within the verify window, definitive mock expiry, duplicate or delayed evidence of a verified capture and an ambiguous inquiry. Verify that the unverified issuer return creates no Avand wallet posting or payout.

This is planned verification. No implementation or test result is claimed.

## When to revisit

Confirm verify, inquiry, reversal and refund semantics against the actual gateway contract.

## Independent-service implications

VerifiedPendingLedger distinguishes known provider success from unresolved wallet delivery. Durable delivery and inquiry converge on one payment action identity even when Ledger recovery runs after the gateway verify window.
