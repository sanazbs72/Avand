---
name: avand-api-security-review
description: Use when reviewing Avand customer ownership, service credentials, financial operation authorization, provider callbacks, staff approvals, or sensitive data exposed by an API.
---

# Avand API Security Review

For each changed route, trace how the caller gains authority over the resource, operation and evidence. Verify those checks on the actual request path before financial effects or sensitive reads.

## Establish the applicable policy

Start with the active feature and the [Constitution](../../../.specify/memory/constitution.md) section XII. Read [Design](../../../docs/DESIGN.md) for customer ownership and payment verification, [Ledger Service](../../../docs/LEDGER_SERVICE.md) for service authority and recovery, and [Financial Operations](../../../docs/FINANCIAL_OPERATIONS.md) sections 3–6 and 9 for producer permissions, evidence and corrections. Check whether authentication, staff approval and provider details are approved before treating them as requirements. Record unresolved mechanisms as contract gaps.

## Follow the authority chain

| Surface | Review check |
| --- | --- |
| Customer commands and reads | Derive CustomerId from the validated identity mapping. Check resource ownership before admission or disclosure, including order status, wallet history, quote, payment, hold and bank destination. A body ID, route ID or idempotency key does not confer authority. Evaluate effective eligibility for new instructions at the appropriate boundary. |
| Application to Ledger | Authenticate the service, derive producer identity from trusted credentials and enforce operation and read scope. Bind customer or staff delegation to the authenticated producer, target and action. Scope operation lookup and replay to authorized callers. Keep Ledger credentials off the frontend and provider callback paths. |
| Provider callbacks and inquiry | Check the approved transport authentication for that provider. Treat a callback as notification and verify the result server-to-server outside the DB transaction. Bind verified evidence to the stored intent and expected provider account, action, amount and asset as applicable. Record the evidence and credit intent durably. Deduplicate callback and inquiry by the same scoped external action. Keep verified late success reviewable after timeout or customer restriction. |
| Withdrawal destination and approval | Verify Sheba ownership against the authenticated customer's national ID and bind that evidence to the requested destination. For an above-ceiling request, verify Finance authority and approval for the exact request and destination before permitting manual payment. Block automatic dispatch for that route. Read the [company clarifications](../../../docs/COMPANY_CLARIFICATIONS.md) and the applicable withdrawal contract. |
| Staff and financial corrections | Authorize support reads and finance writes separately. Where the operation requires maker/checker, validate distinct authorized principals and immutable approvals bound to the case, operation template and evidence digest. Reject self-approval, mismatched approval and changes outside its scope. Route corrections through the typed Ledger operation. |
| Data access and telemetry | Apply minimum DB privileges for each service and role. Keep audit correlation across actor, intent, OperationId, journal and provider evidence. Limit exposed data in response, logs, fixtures and evidence artifacts. Use synthetic inputs for review and avoid logging credentials or raw sensitive provider payloads. |

Evaluate whether a rejection blocks a new request or simply prevents the current caller from learning an earlier result. A later authentication error gives no proof of an earlier financial operation's outcome.

## Verify negative paths

Choose discriminating checks for the affected surface. Use two customer identities to test cross-customer create, read and replay. Use a valid service credential with the wrong operation scope to distinguish authentication from authorization. Try correctly authenticated provider messages with mismatched evidence, a duplicate confirmed capture through callback plus inquiry, and a late verified capture after review. For privileged paths, test forged body IDs, self-approval and an approval whose scope or evidence digest has changed.

Read back the protected resource and durable state. A rejected request must not disclose protected details or create a new order, posting, hold or outbound effect. An authorized duplicate must preserve the single original effect and follow the approved replay contract. Check relevant redaction in actual responses and captured logs where available.

Report the entry point, actor, missing check, reachable effect, exact location and smallest correction. Separate confirmed findings, policy gaps and unexecuted checks. Keep review read-only unless fixes are requested.
