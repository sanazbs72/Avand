# D001 - Fund purchases only from the existing rial wallet

**Status:** Confirmed
**Design checkpoint:** 2026-11-06

[Decision log](../DECISIONS.md) | [Design](../DESIGN.md) | [Defense index](README.md)

## Decision

A customer may buy gold only with their current spendable rial wallet balance. Gateway top-up is an independent flow. A successful top-up never automatically resumes or executes a gold purchase.

## Why this fits V1

This removes payment-network timing from customer purchase execution. All financial legs of a buy remain inside one Ledger transaction, while the Application order converges through durable result recovery. It fits the explicitly chosen wallet-only scope.

## Alternatives considered

- Direct gateway payment for each gold purchase: couples payment verification and quote expiry.
- Automatically resume a purchase after top-up: requires another customer intent and price-confirmation workflow.

## Tradeoffs and limits

A customer with insufficient funds must top up first and submit a new purchase. The original brief's payment-to-gold scenarios require an explicit adaptation.

## Likely defense question

**Why did you separate top-up from buying when the brief mentions gold after a callback?**

The product introduction and my submitted assumption use wallet-funded purchases. I deliberately selected that simpler model and documented the resulting test adaptations rather than claiming literal equivalence.

## Evidence to prepare

A verified top-up changes IRR only. An insufficient-balance buy changes neither IRR nor gold. A later top-up does not execute the rejected buy.

This is planned verification. No implementation or test result is claimed.

## When to revisit

Revisit only if product scope explicitly adds a payment-funded purchase journey.
