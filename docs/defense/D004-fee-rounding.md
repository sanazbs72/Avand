# D004 - Round each calculated rial fee up to a whole rial

**Status:** Confirmed
**Design checkpoint:** 2026-11-06

[Decision log](../DECISIONS.md) | [Design](../DESIGN.md) | [Defense index](README.md)

## Decision

Compute the fee on the actual gold principal using decimal arithmetic and round a fractional-rial fee upward to a whole rial. Persist the quoted rate and resulting fee.

## Why this fits V1

IRR balances use integer units. A single explicit rounding rule gives reproducible quotes and postings.

## Alternatives considered

- Round down: systematically undercollects fractional fees.
- Round to nearest: valid but a different commercial policy.

## Tradeoffs and limits

The rounding increment is less than one rial per fee. This does not set the fee rate, add a fee minimum, or determine principal rounding.

## Likely defense question

**Does ceiling the fee create an undisclosed fee minimum?**

No configured minimum is introduced. Any positive fractional fee rounds to one rial as a consequence of the smallest currency unit, and the quote shows the actual fee.

## Evidence to prepare

Test an exact-rial fee, a fractional-rial fee and zero fee. Replayed requests preserve the stored fee.

This is planned verification. No implementation or test result is claimed.

## When to revisit

Revisit if the commercial fee policy or smallest currency unit changes.
