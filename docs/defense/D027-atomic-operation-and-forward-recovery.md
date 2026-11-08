# D027 - Use one atomic financial operation with durable inquiry and forward recovery

**Status:** Confirmed approach; exact API and state names are working design
**Design checkpoint:** 2026-11-06

[Decision log](../DECISIONS.md) | [Design](../DESIGN.md) | [Ledger service](../LEDGER_SERVICE.md) | [Defense index](README.md)

## Decision

Keep all financial legs, balances, controls and the terminal operation result in one Ledger transaction. Application durably records intent before calling Ledger and applies the known result afterward. Recover Unknown outcomes with the same OperationId; do not reverse a posted trade because Application status persistence failed.

## Why this fits V1

The distributed boundary separates financial finality from Application status, but does not require separate remote debit/credit steps or a compensating workflow for this V1 trade.

## Alternatives considered

- Split debit and credit across remote calls: introduces partial financial states that the Ledger can avoid.
- Treat timeout as failure and retry with a new identity: can execute the same trade twice.
- Deploy a general Saga engine now: not necessary for one atomic financial action plus status convergence.

## Tradeoffs and limits

Application has transient Processing/Unknown states, durable dispatch and result-application records, and a recovery worker. The proposed public POST returns a stable 202 admission response; financial success is obtained from order status. Future multi-service business commitments may still need Saga-style orchestration.

## Likely defense question

**What happens when Ledger commits but Trading never receives the response?**

The durable Application intent retains the original OperationId and immutable request. Recovery queries or retries that identity. Ledger replays its stored result, and Application updates its order and creates procurement work once. A timeout never authorizes a new operation or an automatic reversal.

## Evidence to prepare

Inject response loss after commit and crashes on both Application commits. Verify one financial journal, identical admission responses, eventual correct status, rejection of changed payloads and replay of an already-posted result after quote expiry.

Also send different OperationIds for the same business action/quote and verify the second cannot create another financial effect. A concurrent initial NotFound must not bypass an operation claim or overwrite its Posted result.

This is planned verification. No implementation or test result is claimed.

## When to revisit

Introduce a richer process manager when workflows have several independent business commitments, deadlines, holds or compensations. Do not confuse using a state machine with needing a particular Saga library.
