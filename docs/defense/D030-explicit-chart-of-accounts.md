# D030 - Define typed account templates and separate claims from real backing

**Status:** Detailed account catalog proposed for review
**Design checkpoint:** 2026-11-06

[Decision log](../DECISIONS.md) | [Design](../DESIGN.md) | [Chart of accounts](../CHART_OF_ACCOUNTS.md) | [Financial operations](../FINANCIAL_OPERATIONS.md) | [Defense index](README.md)

## Decision

Adopt the explicit CHART_OF_ACCOUNTS catalog with asset, owner scope, normal side and balance rules for every baseline account. Customer claims are liabilities; confirmed bank/custody assets, noncash receivables, suspense and conversion controls remain distinct.

## Why this fits V1

Fund and Card need the same financial language as Gold. A list of account names without normal-side, ownership and liquidity/backing meaning is not an implementable ledger contract.

## Alternatives considered

- Keep a few illustrative accounts: leaves developers to guess financial semantics.
- Treat receivables or a signed house position as available backing: can authorize unfunded spending.

## Tradeoffs and limits

This is an operational multi-asset ledger, not statutory reporting approval. Suspense cannot create cash/backing. Future fund unit scales and product economics still require activation contracts.

## Likely defense question

**Why are customer balances credits while bank cash is a debit?**

These are Avand's books. Bank cash is its asset; the wallet is its liability to the customer. Per-asset conservation uses signed debit-minus-credit totals, not a sum of positive customer and bank balances.

## Evidence to prepare

Read the 27 baseline templates and the balanced numerical books. Verify that unidentified receipts, pending custody and gateway receivables do not become free cash or eligible gold.

This is planned verification. Static accounting fixtures are not executable application tests.

## When to revisit

Review formal reporting classification with finance; preserve explicit units and ownership when introducing a new asset.
