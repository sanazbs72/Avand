# D016 - Persist dispatch, recovery and external-work intent in the owning service

**Status:** Working mechanism updated for D026-D027
**Design checkpoint:** 2026-11-06

[Decision log](../DECISIONS.md) | [Design](../DESIGN.md) | [Defense index](README.md)

## Decision

Commit Application admission with its immutable Ledger request and recovery identity. Commit Application result application with procurement outbox work. Ledger stores its financial operation result with the journal. V1 recovery queries known OperationIds over HTTP; event publishing is optional, not a required second delivery path.

## Why this fits V1

A crash cannot lose an admitted financial request or create another identity. A second crash after financial success can be repaired before external procurement is scheduled once.

## Alternatives considered

- Fire-and-forget requests: lose intent on process exit.
- Persist procurement only after a separate status commit: leaves a crash window.
- Add a broker and parallel push path immediately: increases infrastructure without being needed for the selected recovery protocol.

## Tradeoffs and limits

The two services have separate commits. Workers need durable claims, retries and backoff. If integration events are introduced, the producer needs an outbox and consumers need deduplication. None of that replaces custodian-side request inquiry/idempotency.

## Likely defense question

**How do you recover without a broker or Saga engine?**

Application has a permanent intent and OperationId. A hosted worker queries/retries it until it obtains Ledger's durable outcome, then applies that outcome and external-work intent once in Application's own transaction.

## Evidence to prepare

Crash after Application admission, after Ledger commit and before Application application. Verify safe recovery and one procurement intent. Separately test unknown custodian execution with a stable external reference.

This is planned verification. No implementation or test result is claimed.

## When to revisit

Introduce a broker only for proven throughput or integration needs.
