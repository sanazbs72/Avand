# D008 - Complete customer trades locally and procure pooled gold asynchronously

**Status:** Confirmed policy; procurement aggregation remains proposed
**Design checkpoint:** 2026-11-06

[Decision log](../DECISIONS.md) | [Design](../DESIGN.md) | [Defense index](README.md)

## Decision

Keep pooled gold at the custodian and individual customer claims internally. Complete an accepted customer buy immediately. Process platform procurement separately, using existing stock and bounded uncovered exposure.

## Why this fits V1

The customer should not wait for a per-user external purchase. Existing platform inventory can satisfy trades, while the custodian operates on pooled holdings.

## Alternatives considered

- Wait for custody purchase before crediting each customer: a different pending-asset product.
- Allocate separate external holdings for every customer: not the chosen pooled model.

## Tradeoffs and limits

The platform owns inventory, external-execution and price-movement exposure. Daily settlement, order execution and report timing must be distinguished.

## Likely defense question

**Why not reverse the customer trade if procurement fails?**

The trade was already final and the customer may have sold the gold. The failure belongs to the platform's procurement and exposure workflow.

## Evidence to prepare

A successful local buy credits customer gold without increasing confirmed custody. A custody timeout does not silently reverse a completed customer trade.

This is planned verification. No implementation or test result is claimed.

## When to revisit

Revisit if physical allocation, regulation or the custodian contract requires full pre-funding.

## Independent-service implications

Financial ownership becomes final when Ledger posts the operation. Application may briefly show Processing or Unknown until its durable coordinator applies that result. Procurement follows that application once; it does not share the Ledger transaction.
