# D014 - Deduplicate both API requests and underlying financial operations

**Status:** Retained requirement; response contract refined by D027
**Design checkpoint:** 2026-11-06

[Decision log](../DECISIONS.md) | [Design](../DESIGN.md) | [Defense index](README.md)

## Decision

Scope the public Idempotency-Key by customer and operation and compare a canonical fingerprint. Store one durable Application order, permanent Ledger OperationId and canonical 202 admission response. Ledger independently deduplicates producer operations and financial business references.

## Why this fits V1

API retries and repeated external notifications are different duplication paths and need distinct durable identities.

## Alternatives considered

- An in-memory key cache: loses protection across processes and restarts.
- Only deduplicate HTTP keys: does not protect callbacks with distinct delivery identities.

## Tradeoffs and limits

Concurrent same-key requests replay the same admission response, not a changing order-status response. Financial finality is obtained through status and the durable Ledger outcome. Transport timeouts remain possible. Public key retention must not remove financial-operation deduplication.

## Likely defense question

**Can exactly the same response reach all 100 callers?**

The controlled test asserts one order and an identical stored 202 with OrderId/StatusUrl, then one eventual financial result. Network failures can prevent delivery, so retry recovers that acknowledgement; it does not create a new order.

## Evidence to prepare

100 concurrent identical admitted buys produce one order, identical admission responses and one eventual financial journal. A changed payload under either stable identity conflicts. Repeated payment callbacks/inquiries converge on one financial posting.

This is planned verification. No implementation or test result is claimed.

## When to revisit

Specify key retention, response fields and timeout behavior before API implementation.
