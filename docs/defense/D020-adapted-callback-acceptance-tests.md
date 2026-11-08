# D020 - Explicitly adapt callback acceptance scenarios to independent wallet top-up

**Status:** Confirmed deliberate deviation from literal brief wording
**Design checkpoint:** 2026-11-06

[Decision log](../DECISIONS.md) | [Design](../DESIGN.md) | [Defense index](README.md)

## Decision

Test duplicate evidence of a verified capture as one IRR wallet credit. Test late and missing callbacks against the top-up lifecycle, including definitive mock verification expiry and late delivery of already verified evidence. Test quote expiry separately against customer trade confirmation.

## Why this fits V1

The customer explicitly authorized adapting these scenarios to the selected wallet-only V1 model.

## Alternatives considered

- Reintroduce payment-linked buys: conflicts with the chosen scope.
- Quietly rename expected outcomes: conceals a real change to the assignment.

## Tradeoffs and limits

These are not literally identical to the original callback-to-gold scenarios. An evaluator may enforce the original wording despite the documented interpretation.

## Likely defense question

**Did you meet every original scenario unchanged?**

No. I retained the safety objectives but intentionally changed payment-linked expectations. The decision log identifies that change and its acceptance risk.

## Evidence to prepare

Keep an original-to-adapted scenario matrix. Demonstrate duplicate top-up safety, trade idempotency, expiry, inquiry and review behavior separately.

This is planned verification. No implementation or test result is claimed.

## When to revisit

Revisit only if the evaluator requires literal payment-to-gold coupling.
