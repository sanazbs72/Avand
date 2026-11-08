# D031 - Specify typed financial and control operations with replay-safe outcomes

**Status:** Detailed baseline contract proposed under the confirmed Ledger service direction
**Design checkpoint:** 2026-11-06

[Decision log](../DECISIONS.md) | [Design](../DESIGN.md) | [Chart of accounts](../CHART_OF_ACCOUNTS.md) | [Financial operations](../FINANCIAL_OPERATIONS.md) | [Defense index](README.md)

## Decision

Use the versioned FINANCIAL_OPERATIONS contract for baseline financial templates and no-journal controls. Define payloads, posting patterns, guards, holds, durable outcomes, business/external action identities and recovery behavior instead of accepting raw journal lines.

## Why this fits V1

An independent service boundary needs an executable contract, not only an architectural diagram. Typed operations keep account selection and critical guards inside Ledger.

## Alternatives considered

- Expose arbitrary debit/credit arrays to Application: transfers financial ownership to callers.
- Define only happy-path buys: leaves refund, custody and correction failures unspecified.
- Treat every timeout or duplicate evidence as payment failure: confuses transport knowledge with financial fact.

## Tradeoffs and limits

The contract is broader than the first implementation slice. Unsupported commands must be rejected explicitly, not simulated as Posted. Provider evidence is verified in Application and must remain traceable; no real gateway contract is invented.

## Likely defense question

**How do capture, settlement and refund avoid crediting or returning the same money twice?**

Capture and each external return have durable action identities and a locked per-payment financial state. Settlement moves a receivable to cash, not another wallet credit. Refund reserves the captured entitlement and consumes it once; known earlier returns can be applied atomically before any spendable wallet credit.

## Evidence to prepare

Run the posting fixture checks, then planned two-service tests for duplicate identities, already-returned capture, unknown holds, partial settlement and response loss. Static accounting checks do not claim runtime correctness.

This is planned verification. Static accounting fixtures are not executable application tests.

## When to revisit

Version the contract for future Fund/Card provider commitments and map actual external evidence without weakening old replay semantics.
