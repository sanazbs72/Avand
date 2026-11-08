# D035 - Authorize Card spending through atomic funded holds and separate bank finality

**Status:** Future product design proposal; V1 implementation excluded

[Design](../DESIGN.md) | [Defense index](README.md)

## Decision

Design Card authorization, liquidation, capture, release, reversal and settlement on shared Ledger controls. Use IRR first, then consented gold; Fund fallback remains disabled without a funded ownership-transfer arrangement. No Card implementation is included in V1.

## Why this fits V1

The switch deadline cannot wait for custody or brokerage. A common financial hold prevents Card and Gold from independently spending the same assets. The take-home needs these product designs, not their code.

## Alternatives considered

- Treat pending external activity as final assets/cash: loses settlement correctness.
- Implement the full future product now: consumes the small team's limited V1 delivery time.

## Tradeoffs and limits

Partner response deadlines, late presentment and cancellation finality are activation contracts. Independent services require durable unknown-outcome recovery; no offline spending or free bridge credit is assumed.

## Likely defense question

**Does cancelling a card purchase restore sold gold?**

No. The liquidation was a real completed financial trade. Release restores IRR; buying assets again needs a new quote. Unknown authorization is not evidence that a hold can be released.

## Evidence to prepare

Lost approval response, concurrent spend, stale quote, liquidity shortage, capture/release race, partial captures and duplicate refunds. These are planned acceptance cases, not executed runtime tests.

## When to revisit

Validate actual partner contracts and funded limits before product activation.
