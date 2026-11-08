# Avand V1 - Independent Ledger Service

Date: 2026-11-06
Status: Independent deployment and atomic-operation/recovery direction confirmed. Detailed protocol choices below are the author's working design, not implemented behavior.

[Design](DESIGN.md) | [Decisions](DECISIONS.md) | [Project structure](PROJECT_STRUCTURE.md)
[Service defense D026](defense/D026-independent-ledger-service.md) | [Recovery defense D027](defense/D027-atomic-operation-and-forward-recovery.md)
[Complete draft Chart of Accounts](CHART_OF_ACCOUNTS.md) | [Complete draft operation contract](FINANCIAL_OPERATIONS.md) | [Posting examples](LEDGER_POSTING_EXAMPLES.md)

## 1. Deployment and data ownership

There are exactly two planned backend deployments:
1. Application API: Customers, Identity integration, Pricing, Gold Trading, Payments, Custody integration, Treasury operations and Reconciliation.
2. Ledger API: authoritative financial accounts, journals, balances, operation outcomes and atomic financial controls.

Fund and Card are future Application API capabilities in this topology, not additional V1 services.

Use two separately owned PostgreSQL databases and credentials. A single PostgreSQL instance/container may host both for the exercise. That shares database-engine availability and is not full failure isolation. Neither service reads or writes the other's tables or shares DbContext, DbConnection or DbTransaction. Transactions are local to each database.

An internal synchronous HTTP API is the V1 communication mechanism. Persist intent and recover in the background. No message broker, service mesh, Saga engine or distributed transaction is required for this slice.

## 2. Financial boundary

Ledger owns:
- Per-asset accounts, immutable journals and synchronous balances.
- One durable outcome for each producer operation.
- Nonnegative customer spendable balances and any explicit holds.
- The authoritative gold-backing and cash-capacity state needed to accept a financial operation.
- Financial eligibility restrictions effective at Ledger acceptance.
- Audit references for confirmed funding/custody movements and corrections.

Application modules own:
- Customer intent and product order lifecycle.
- Quote issuance, price-provider integration and presentation.
- Verification of gateway/bank/custodian evidence.
- External requests, inquiry, procurement, reconciliation and operations assignment.
- Selection of commercial policies; Ledger atomically enforces their supported financial limits.

Ledger does not obtain prices, call the gateway/custodian/broker, or decide which product a customer should buy. It accepts only authorized, typed financial operations with a trusted immutable valuation snapshot. Do not expose arbitrary account IDs and unrestricted journal construction to customer endpoints.

Application balances may be cached for display but are not authoritative for spending.

## 3. Financial and control contracts

FINANCIAL_OPERATIONS is now the authoritative draft for exact routes, schemas, 21 financial types, 11 control types, normal-side posting, durable outcomes and error codes. The following is an architectural summary:
- POST /internal/v1/operations: execute a typed operation.
- GET /internal/v1/operations/{operationId}: retrieve a durable result, scoped to the authenticated producer.
- GET /internal/v1/customers/{customerId}/balances: retrieve authoritative balances.
- Authorized administrative contracts for eligibility, policy and verified financial-fact updates.

A financial request carries:
ProducerId (derived from service credentials), OperationId, BusinessReference, OperationType, CustomerId, actor/correlation references, immutable amounts/asset identifiers, and where applicable QuoteId, quote snapshot and ValidUntil.

ProducerId is not accepted as authority merely because it appears in JSON. Authenticate the caller and authorize its operation types. Ledger maps supported operations to allowed account patterns.

The durable deduplication identity is (ProducerId, OperationId). Also protect the corresponding business reference/action and successful quote usage against duplication. Reuse with a different canonical payload is Conflict.

Results include Posted or Rejected, OperationId, the immutable decision, journal references when posted, and the time of the Ledger decision. Persist terminal business rejections too; a later wallet top-up must not automatically turn an earlier insufficient-balance operation into a successful trade.

Operation identity remains durable with the financial record. Pruning an HTTP retry cache must not make an old financial operation executable again.

## 4. One atomic Ledger transaction

For a new valid operation:
1. Authenticate/authorize the producer and check for an existing operation result.
2. Begin a Ledger transaction and claim the durable identity.
3. Acquire eligibility, account and shared financial-control locks in deterministic order.
4. Recheck the financial eligibility state and applicable policy versions.
5. For a quoted operation, enforce ownership/reference and single use, and require current server time < ValidUntil at the protected decision point.
6. Check all funding, backing and liquidity conditions.
7. Post all financial legs and fees, update balances and capacity, and persist the terminal operation result.
8. Commit, then return the stored result.

An existing identical operation returns its stored result before considering current expiry or a later customer restriction. This replays a prior fact; it does not authorize a new trade.

The initial result lookup is only a fast path. If another request wins the unique operation claim, wait/retry under the selected database protocol and read its committed result, comparing the payload before returning it. An earlier NotFound must not cause a second posting or an expiry rejection of an operation that has since Posted. A different OperationId cannot bypass uniqueness of the same business action or successful quote use.

For a buy, the IRR debit, gold credit and fee are one transaction. For a sell, the gold debit, net IRR credit, fee and liquidity consumption are one transaction. Do not split these into separate remote debit/credit calls.

No provider network call or Application database write occurs inside this transaction. A technical failure before commit rolls back. A business rejection persists its result without financial entries.

## 5. Durable Application workflow and stable client response

Proposed public contract:
- A structurally valid, authorized request is durably admitted before any Ledger call.
- POST buy/sell returns the same stored 202 response for the same customer/operation Idempotency-Key: OrderId and StatusUrl.
- The 202 means accepted for processing, not that money has moved.
- GET order status provides the evolving business state and final financial result.
- Invalid authentication, malformed input or key/payload mismatch may be rejected before admission.

Application database transaction A:
- Claim the external Idempotency-Key and request fingerprint.
- Create one order and assign its permanent Ledger OperationId.
- Claim the quote for that intent; do not reuse it in another admitted order.
- Store the immutable financial request and dispatch/recovery record.
- Store the canonical admission response.
- Commit.

The API may dispatch immediately after commit for low normal-case latency. A hosted recovery worker owns eventual completion if that attempt is lost or interrupted.

After a definitive Ledger result, Application database transaction B:
- Apply the result once to the matching order.
- Save the Ledger operation/journal references and application marker.
- For a posted gold buy, create the procurement intent/outbox record once.
- Commit.

Ledger Posted is financial finality. Application status may lag until transaction B; the delay must be visible rather than interpreted as a financial failure. Normal operation can still feel immediate, but the design does not promise zero processing time.

This admission response refines the earlier same-final-response proposal. The 100-request test asserts one order and identical admission responses, then verifies one eventual financial result and posting.

## 6. Unknown outcomes and forward recovery

| Failure | Required behavior |
| --- | --- |
| Application crashes before transaction A commits | No durable acceptance; retry the same public key |
| Transaction A commits, dispatch never happens | Worker dispatches the saved immutable operation |
| Ledger fails before commit | Retry the same OperationId; no partial financial effect |
| Ledger commits, response is lost | Keep the order unresolved; query/retry the same OperationId |
| Ledger result is known, Application transaction B fails | Retry applying that same result locally |
| Application completes but HTTP response is lost | Replay the canonical admission response; read status |
| Inquiry returns NotFound | Do not infer definitive failure; the first attempt may be in flight |
| Quote expires before the first successful Ledger decision | Store Rejected; require a new quote and customer confirmation |
| Replay arrives after expiry but the operation already Posted | Return the original Posted result |
| Same identity arrives with changed amount/fee | Conflict, no new financial effect |

Proposed order states: Processing, FinancialOutcomeUnknown, Completed, Rejected. Unknown and Processing create no permission to reuse funds or send a replacement OperationId.

Do not generate a new operation identity on retry. Do not compensate a Posted trade merely because an order-status update failed. Do not automatically reprice an admitted request.

Use bounded request timeouts, backoff and persistent recovery scheduling. Stopping an individual retry attempt is not the same as declaring the financial operation failed.

The baseline recovery path is polling/querying known OperationIds from durable Application records. A broker or push-event delivery path is not required. If integration events are later published, commit them through the emitting service's own outbox and deduplicate them at the receiver.

## 7. Quote and eligibility races

The Application API validates the quote and sends its immutable authorized financial snapshot. Ledger rechecks ValidUntil when it can actually accept the operation. A Application-side precheck alone cannot authorize a delayed request.

The exact economics captured in an admitted operation are immutable. Reject a payload change under the same identity. FINANCIAL_OPERATIONS section 2 records the confirmed configurable order limits and side-specific fees, along with proposed arithmetic details. Ledger validates the versioned policy and server-issued expiry at first execution.

Working eligibility protocol:
- Customers owns the profile and desired Active/Verified state.
- An idempotent, monotonically versioned financial-eligibility command updates Ledger's authoritative acceptance gate.
- The Application UI must distinguish a requested restriction from an effective, Ledger-acknowledged restriction.
- New financial acceptance and a financial restriction serialize inside Ledger; the one committed first defines the ordering.
- Until Ledger acknowledges an activation/verification, new financial operations remain disabled.
- A Application restriction request immediately blocks new local initiation, but must not claim global effectiveness before Ledger acknowledgement.
- Existing verified external results use an authorized recovery operation and remain accountable after suspension.

Application commits the desired eligibility state, its monotonically increasing version, stable control CommandId and delivery intent together. The recovery worker retries that command/version or reads Ledger's effective version after a lost acknowledgement. Ledger commits the authoritative state and deduplicated command outcome atomically. Application only marks the matching desired version effective; an old acknowledgement cannot mark a newer request applied. Activation remains fail-closed while its effective version is unresolved. Apply the same durable intent, command identity and read-back recovery to authorized policy/control updates; version updates per target and deduplicate individual confirmed financial movements by their external action references.

This explicit effective-time boundary replaces the earlier implicit assumption that profile and posting share a database lock. Onboarding implementation remains open. The financial draft now proposes Restricted credit for earlier real funds received after restriction and an explicit approved release operation. The protocol must not silently use an eventually consistent customer copy while claiming immediate cross-service revocation.

## 8. Backing and liquidity guards

Working authority choice [D028](defense/D028-ledger-financial-control-authority.md): the state that authorizes spending is inside the same Ledger transaction as posting.

The agreed 1% gold formula is unchanged for its agreed outage scope. Ledger has authoritative U from customer gold accounts and confirmed C from deduplicated custody facts. Custody owns obtaining evidence, not directly editing C in another database. A pending purchase does not increase C.

Treasury chooses commercial liquidity policies and arranges funds. Ledger owns confirmed cash, financial commitments/holds and the spendable liquidity control used when posting a sale.

All producers, including future Fund/Card, must use these controls. A read in Application followed by an unguarded remote post is insufficient.

Known cash/gold outflows require a protected Ledger authorization/hold before external dispatch; an eventual callback alone cannot prevent the capacity from being spent in the meantime. Apply final confirmed movements once using stable external references. Incoming unconfirmed movements create no capacity.

CHART_OF_ACCOUNTS sections 6-7 now define draft obligation groups, represented versus unrepresented reservations, physical cash availability and conservative outgoing-gold capacity. The healthy-mode 1% policy is a new explicit review proposal. FINANCIAL_OPERATIONS defines hold creation, partial consumption and safe release. Do not substitute an informal bank-balance approximation. Out-of-band movements enter evidenced suspense/finance workflows, not silent capacity edits.

## 9. Independent top-up and procurement

Payments verifies the gateway result and durably saves the verification evidence plus a Ledger-credit intent. It calls Ledger using a stable payment action identity. A callback and an inquiry for the same payment converge on that identity.

Proposed top-up states add VerifiedPendingLedger. Provider success is known there, but wallet posting is unresolved. Mark Succeeded only after Ledger confirms the credit. Ledger unavailability is not gateway failure and must not trigger an automatic refund.

The company-confirmed mock gateway rejects verification after 15 minutes. Application receives callbacks at any time, inquires when the result is uncertain, and records a definitive expired verification as Failed without a Ledger credit. The issuing bank handles the return of unverified card payments within the stated 72 hours. A capture already verified with the gateway follows the separate Ledger delivery backlog and can post once after restriction under the applicable bucket policy. See [company clarifications](COMPANY_CLARIFICATIONS.md).

A posted customer buy causes procurement only when its financial result has been applied durably in Application. That application and the procurement outbox share Application transaction B. Customer finality does not wait for custody execution. Custodian retries still need provider-specific deduplication/inquiry.

## 10. Fund and Card extension boundary

CHART_OF_ACCOUNTS section 9 and FINANCIAL_OPERATIONS section 12 now provide the ledger-side account/operation extension templates. They do not finalize future product activation.

Prepare Ledger contracts for multiple assets and financial holds; do not implement the full Fund/Card products in V1.

Fund integration will need available/reserved units and pending cash claims. A redemption receivable is not cash. Unit precision must be defined per asset; GOLD_MG does not define fund precision.

Card integration will need reservation/authorization, capture/release and strict shared spending control. Converting a customer's gold into IRR can be one atomic Ledger operation once its product valuation and liquidity authorization are valid.

Selling fund units cannot by itself create cash within the card-switch deadline when brokerage settlement takes one to two business days. A funded liquidity/credit arrangement or a rejection policy is still a necessary design decision. Do not hide this behind generic ledger posting.

These are capability requirements for later detailed design, not implemented V1 endpoints or proof that a Saga is unnecessary for every future workflow.

## 11. Deployment, people and time

The brief gives 72 hours total and starts engineering with one backend-capable lead, one senior frontend engineer and a designer. It does not provide three backend engineers. Elapsed assignment time and actual delivery estimates have not been supplied.

Keep scope bounded:
- Two .NET backend hosts in one repository; common build conventions and lightweight versioned DTO contracts.
- Two owned databases on one PostgreSQL instance for the local demo.
- One frontend app calling Application API; it never receives Ledger service credentials.
- Hosted workers, durable database records and HTTP; no mandatory broker/Saga engine.
- No separate Pricing, Customer, Gold, Fund, Card or Treasury deployments in V1.
- No full event-sourcing platform or generic multi-product workflow engine.
- No full Fund/Card implementation.

Ownership proposal:
- Lead: Ledger, financial controls, Application coordinator/recovery, adapters, migrations and financial integration tests.
- Frontend engineer: quote, trade/payment status, history and support views against agreed API contracts.
- Designer: clear processing/unknown/review messages and demo flow.

The first demonstrable engineering slice must include response-loss recovery and concurrent posting, not just a successful HTTP call. Size later work from that evidence. Do not assign invented hours or claim measured performance.

If time becomes tight, reduce visual polish and optional infrastructure/features before relaxing atomic posting, idempotency or recovery. Record honest omissions rather than adding shallow services. The remaining business-policy gaps in DESIGN still block a complete build plan.

## 12. Evidence and operational checks

Planned tests:
- 100 same-key public buys: one order, identical admission response, one financial result.
- Lost response after Ledger commit: one journal and eventual correct Application status.
- Application crash after admission and after Ledger success: recovery completes safely.
- Expired new operation rejects; already-posted operation replays after expiry.
- Different payload under the same operation identity is rejected.
- Simultaneous Card-shaped and Gold-shaped spending requests cannot share the same funds.
- Concurrent buys respect shared backing; concurrent sales respect shared liquidity.
- Late verified top-up during Ledger outage is eventually credited once.
- Restriction versus trade has the defined Ledger ordering.
- A crash before restriction dispatch and a lost control-update acknowledgement both recover; an older acknowledgement cannot make a newer desired state effective.
- Different OperationIds referencing the same business action or successful quote cannot create another posting.
- Neither database credential can write the other's data.
- Reconciliation detects financial posting versus Application status discrepancies as well as external mismatches.

Operational measurements:
Ledger request/commit latency, timeout rate, posting rejections, lock waits, oldest unresolved Application intent, recovery retries, VerifiedPendingLedger age and procurement backlog.

Ledger unavailability stops new financial execution; independent deployment does not make products financially autonomous. Reads must distinguish confirmed state from cached display data.

No code, migrations, deployment or executable tests are delivered by this document.
