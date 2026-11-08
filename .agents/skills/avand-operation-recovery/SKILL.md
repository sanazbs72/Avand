---
name: avand-operation-recovery
description: Use when designing, implementing, or reviewing Avand retries, idempotency, lost Ledger responses, crash recovery, duplicate callbacks, unknown provider outcomes, or release of potentially dispatched holds.
---

# Avand Operation Recovery

Recover the original financial decision from durable evidence. Track Application admission, Ledger financial finality and external provider finality separately.

## Locate the contract

Read the active feature specification and the [Constitution](../../../.specify/memory/constitution.md). Use [Ledger Service sections 5–7](../../../docs/LEDGER_SERVICE.md) for Application admission, forward recovery and races. Use [Financial Operations sections 3–7, 11 and 15](../../../docs/FINANCIAL_OPERATIONS.md) for identity, result semantics, payment evidence, holds and refund conflicts. Check the status of each design detail before treating it as binding. Surface gaps in provider guarantees or approved policy for the affected path.

## Follow the original operation

1. **Admit durably before dispatch.** In Application transaction A, persist the public idempotency claim and fingerprint, immutable financial request, permanent Ledger OperationId and recovery intent. Store the order, quote claim and canonical admission acknowledgement where the approved contract calls for them. A replay returns the same admission acknowledgement. Order status carries the evolving financial outcome.
2. **Use the correct identity.** Distinguish the customer-scoped public key, producer-scoped Ledger OperationId, business action key and provider-scoped external action reference. Apply durable uniqueness at each relevant boundary. Compare canonical economic content on replay. A conflict or duplicate-business-action response points to the original decision and does not authorize another effect.
3. **Resolve from Ledger.** Persist the financial decision with its effects. If dispatch or acknowledgement is uncertain, query or safely replay the same immutable operation. Timeout, 503, cancellation, missing callback, retry exhaustion and ordinary NotFound leave an earlier attempt unresolved. Persist the recovery schedule and use bounded attempts with backoff. Surface stuck work for reconciliation while retaining its identity and protections.
4. **Apply a definitive result forward.** On Posted, retry Application transaction B until the matching order, result references, apply marker and required follow-up intent are saved together. For a posted buy, create procurement intent once in that transaction. Ledger Posted is financial finality. An Application failure calls for local repair, not compensation or replacement. Apply a stored business rejection as final for that operation.
5. **Separate a replay from a first decision.** A stored Posted or Rejected result replays even if quote, policy or eligibility has changed. An unresolved request keeps its original terms. If Ledger reaches its first decision after the original quote expires, apply the approved expiry rule and store the rejection. A replacement trade requires a definitive original outcome and fresh customer authorization where applicable.

## Recover provider effects and holds

- Persist the external request identity and DispatchStarted state before the network call. A crash after the marker can leave the outcome unknown even if the provider lookup returns NotFound. Inquire using the original reference. Resend only when the provider contract makes same-reference retry safe. Otherwise retain protection and escalate for definitive evidence.
- Keep unresolved remaining exposure reserved. Release requires safe pre-dispatch cancellation or authoritative evidence the remaining amount cannot settle. Serialize capture, partial settlement and release on the same hold.
- Verify callback evidence and deduplicate by the scoped provider action identity before posting. Apply only new facts in a mixed batch and flag conflicting evidence. An earlier timeout or review state does not erase verified late success.
- Handle callbacks regardless of arrival time and resolve the outcome through server-to-server verification or inquiry. Use the gateway verify window in the [company clarifications](../../../docs/COMPANY_CLARIFICATIONS.md) and the active contract. A definitive expired verification produces Failed with no wallet credit or Avand refund dispatch. The issuer handles the unverified card return. An ambiguous reply stays unresolved. VerifiedPendingLedger recovers already verified evidence under its original identity, including after the gateway window closes.
- When an independent provider reversal satisfies a refund entitlement but the refund might already have been dispatched, stop further dispatch and follow the OutcomeConflict contract. Track customer entitlement and remaining external exposure separately until definitive settlement or cancellation.

## Verify the failure windows

Exercise the affected path across the actual service boundary with durable stores. Restart the worker or service at the relevant interruption. Assert both the absence of duplicate effects and eventual application of the original decision.

| Inject | Observe |
| --- | --- |
| Crash after Application admission, before dispatch | Recovery sends the saved request under the original identity |
| Ledger commits, response is lost, quote expires | Same-ID inquiry or replay returns the original result with one financial posting |
| No decision exists yet and original quote expires | First decision follows the expiry contract and stores a rejection |
| Ledger result is known, Application update rolls back | Local recovery applies it once and emits required follow-up intent once |
| Duplicate, delayed or conflicting provider evidence | Verified facts post once and conflicting evidence follows the contract |
| Provider outcome unknown after dispatch marker | Hold remains protected until supported settlement or safe release |

Report the last durable state, identity used for recovery, permitted next action, and evidence for the result. Mark unexecuted checks as unrun.
