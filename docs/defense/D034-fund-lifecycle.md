# D034 - Model Fund allocation and delayed cash as explicit pending claims

**Status:** Future product design proposal; V1 implementation excluded

[Design](../DESIGN.md) | [Defense index](README.md)

## Decision

Design Fund subscription/redemption with customer reservations, brokerage evidence, per-fill identities and explicit nonwithdrawable pending claims. Application owns business-calendar/cutoff and external recovery; Ledger owns units/cash. No Fund code is included in V1.

## Why this fits V1

Daily NAV is not a guaranteed execution price, and one-to-two-business-day redemption cannot be shown as immediately spendable cash. The take-home needs these product designs, not their code.

## Alternatives considered

- Treat pending external activity as final assets/cash: loses settlement correctness.
- Implement the full future product now: consumes the small team's limited V1 delivery time.

## Tradeoffs and limits

Brokerage precision, fees, cancellation finality and calendar must be confirmed before activation. Proposed commercial limits are review choices, not existing agreements.

## Likely defense question

**Why not credit redemption cash when units are submitted?**

Submission is not redemption confirmation, and confirmation is not cash receipt. Show reserved units, then a pending receivable, then available cash only after actual settlement.

## Evidence to prepare

Duplicate/partial fills, cutoff/holiday dates, cancel versus execution, overdue cash and restricted receipt. These are planned acceptance cases, not executed runtime tests.

## When to revisit

Validate actual partner contracts and funded limits before product activation.
