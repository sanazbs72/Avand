# Avand V1 - Decision Log

Date: 2026-11-06
Status: Record of the discussion, with explicit confidence and approval boundaries.

## Status legend

- **Confirmed**: explicitly agreed in the discussion.
- **Retained requirement**: required by the original brief and not intentionally removed.
- **Working proposal**: discussed or recommended, but not separately approved as a final implementation choice.
- **Source assumption**: inherited from Avand-Questions_en.md; not independently verified.
- **Superseded**: a historical proposal replaced by a later decision; retained to explain the tradeoff, not as the active architecture.

A mixed status means the policy is agreed while its mechanism or detailed account model is still proposed.

## Index

| ID | Decision | Status | Defense |
| --- | --- | --- | --- |
| D001 | Fund purchases only from the existing rial wallet | Confirmed | [Read](defense/D001-wallet-only-purchases.md) |
| D002 | Treat the entered purchase amount as gold principal, with an additional fee | Confirmed | [Read](defense/D002-principal-plus-fee.md) |
| D003 | Round traded gold down to whole milligrams and charge for the actual quantity | Confirmed | [Read](defense/D003-milligram-rounding.md) |
| D004 | Round each calculated rial fee up to a whole rial | Confirmed | [Read](defense/D004-fee-rounding.md) |
| D005 | Separate a 30-second quote lock from a 30-second feed freshness limit | Confirmed | [Read](defense/D005-quote-lifetime-and-feed-freshness.md) |
| D006 | Use an append-only double-entry ledger as the financial source of truth | Confirmed principle; detailed D030 account catalog proposed for review | [Read](defense/D006-double-entry-ledger.md) |
| D007 | Maintain balances synchronously in the ledger transaction | Working proposal supporting the agreed ledger model | [Read](defense/D007-transactional-balance-projection.md) |
| D008 | Complete customer trades locally and procure pooled gold asynchronously | Confirmed policy; procurement aggregation remains proposed | [Read](defense/D008-pooled-custody-and-immediate-trades.md) |
| D009 | Bound uncovered gold by 1% of total confirmed custody | Confirmed outage policy; D032 healthy-mode/reservation defaults proposed | [Read](defense/D009-one-percent-backing-limit.md) |
| D010 | Display an advisory maximum and enforce capacity again at confirmation | Confirmed | [Read](defense/D010-display-maximum-buy.md) |
| D011 | Accept a gold sale only when its net proceeds can be immediately withdrawable | Confirmed | [Read](defense/D011-liquidity-gated-sales.md) |
| D012 | Replenish liquidity through operations, with no automatic custody sale | Confirmed | [Read](defense/D012-manual-treasury.md) |
| D013 | Earlier unresolved top-up review policy | Historical 20-minute rule superseded by D036. Verified capture still posts once | [Read](defense/D013-payment-review-and-late-success.md) |
| D014 | Deduplicate both API requests and underlying financial operations | Retained requirement; response contract refined by D027 | [Read](defense/D014-idempotency-and-business-deduplication.md) |
| D015 | Keep financial effects atomic inside Ledger and recover Application status separately | Financial invariant retained; earlier cross-module transaction superseded by D026-D027 | [Read](defense/D015-atomic-local-posting-and-locks.md) |
| D016 | Persist dispatch, recovery and external-work intent in the owning service | Working mechanism updated for D026-D027 | [Read](defense/D016-durable-outbox-and-workers.md) |
| D017 | Keep customer trades, top-ups, custody operations and treasury requests separate | Confirmed separation; exact state names proposed | [Read](defense/D017-separate-operation-lifecycles.md) |
| D018 | Use C# on .NET 10 and PostgreSQL | Confirmed stack; EF Core 10 and Npgsql 10 proposed | [Read](defense/D018-dotnet-postgresql-stack.md) |
| D019 | Use a modular monolith and one transactional database for V1 | Superseded historical proposal; replaced by D026 | [Read](defense/D019-modular-monolith.md) |
| D020 | Explicitly adapt callback acceptance scenarios to independent wallet top-up | Confirmed deliberate deviation from literal brief wording | [Read](defense/D020-adapted-callback-acceptance-tests.md) |
| D021 | Retain bidirectional reconciliation and traceable operation history | Retained requirement; detailed workflow remains proposed | [Read](defense/D021-reconciliation-and-history.md) |
| D022 | Use exact asset units and a small journal schema | Working proposal | [Read](defense/D022-exact-units-and-posting-model.md) |
| D023 | Separate Customer, Identity and ledger ownership | Confirmed separation; entity fields and identity mechanism proposed | [Read](defense/D023-customer-identity-and-ledger-ownership.md) |
| D024 | Require Active and Verified status for new customer financial instructions | Confirmed policy; coordination mechanism proposed | [Read](defense/D024-active-verified-financial-operations.md) |
| D025 | Use three backend projects with business modules inside Core | Superseded historical folder proposal; replaced by D026 and revised PROJECT_STRUCTURE | [Read](defense/D025-project-layout-and-dependencies.md) |
| D026 | Deploy Ledger independently as the shared financial capability | Confirmed direction; protocol and project details are working design | [Read](defense/D026-independent-ledger-service.md) |
| D027 | Use one atomic financial operation with durable inquiry and forward recovery | Confirmed approach; exact API and state names are working design | [Read](defense/D027-atomic-operation-and-forward-recovery.md) |
| D028 | Enforce spending, backing, liquidity and effective financial restrictions inside Ledger | Working authority design preserving confirmed policies | [Read](defense/D028-ledger-financial-control-authority.md) |
| D029 | Bound the service split to the available team and assignment time | Delivery proposal under the confirmed service direction | [Read](defense/D029-bounded-two-service-delivery.md) |
| D030 | Define typed account templates and separate claims from real backing | Detailed account catalog proposed for review | [Read](defense/D030-explicit-chart-of-accounts.md) |
| D031 | Specify typed financial and control operations with replay-safe outcomes | Detailed baseline contract proposed under the confirmed Ledger service direction | [Read](defense/D031-typed-financial-operation-contracts.md) |
| D032 | Make numeric and capacity defaults explicit for review | Author-proposed defaults; prior confirmed policies retained | [Read](defense/D032-exact-rounding-and-financial-guard-defaults.md) |
| D033 | Restrict journal writes and verify projections inside the Ledger transaction | Database enforcement and lock strategy proposed | [Read](defense/D033-controlled-journal-posting-and-projections.md) |

| D034 | Model Fund allocation and delayed cash as explicit pending claims | Future product design proposal; V1 implementation excluded | [Read](defense/D034-fund-lifecycle.md) |
| D035 | Authorize Card spending through atomic funded holds and separate bank finality | Future product design proposal; V1 implementation excluded | [Read](defense/D035-card-authorization-and-settlement.md) |
| D036 | Apply company-confirmed V1 product rules | Company-confirmed Q1–Q17. Detailed interpretations are recorded separately | [Read](defense/D036-company-confirmed-v1-rules.md) |

## Decision records

### D001: Fund purchases only from the existing rial wallet

**Status:** Confirmed

A customer may buy gold only with their current spendable rial wallet balance. Gateway top-up is an independent flow. A successful top-up never automatically resumes or executes a gold purchase.

**Boundary / consequence:** A customer with insufficient funds must top up first and submit a new purchase. The original brief's payment-to-gold scenarios require an explicit adaptation.

[Defense and planned evidence](defense/D001-wallet-only-purchases.md)

### D002: Treat the entered purchase amount as gold principal, with an additional fee

**Status:** Confirmed

The entered rial amount represents gold value, not a fee-inclusive budget. At confirmation, the wallet must cover the final rounded gold value plus the buy fee. Reject the entire buy if it cannot.

**Boundary / consequence:** A wallet equal to the entered principal is insufficient when the fee is positive. UI must display principal, fee and total before confirmation.

[Defense and planned evidence](defense/D002-principal-plus-fee.md)

### D003: Round traded gold down to whole milligrams and charge for the actual quantity

**Status:** Confirmed

For rial-based buys, round the resulting weight down to a whole milligram. Recalculate principal and fee for that actual quantity. Leave the unused rial remainder in the wallet.

**Boundary / consequence:** A small part of the entered principal can remain unspent. Fractional-rial principal rounding still needs an explicit policy.

[Defense and planned evidence](defense/D003-milligram-rounding.md)

### D004: Round each calculated rial fee up to a whole rial

**Status:** Confirmed

Compute the fee on the actual gold principal using decimal arithmetic and round a fractional-rial fee upward to a whole rial. Persist the quoted rate and resulting fee.

**Boundary / consequence:** The rounding increment is less than one rial per fee. This does not set the fee rate, add a fee minimum, or determine principal rounding.

[Defense and planned evidence](defense/D004-fee-rounding.md)

### D005: Separate a 30-second quote lock from a 30-second feed freshness limit

**Status:** Confirmed

Issue a quote only when the latest valid provider price is no more than 30 seconds old. A quote remains valid for 30 seconds from issuance. Stop issuing new quotes on a stale feed, but honor already-issued quotes until their own expiry. An expired quote requires a new quote and explicit customer confirmation.

**Boundary / consequence:** A quote issued near the freshness limit can be honored when the underlying price is almost 60 seconds old. That bounded exposure is intentional.

[Defense and planned evidence](defense/D005-quote-lifetime-and-feed-freshness.md)

### D006: Use an append-only double-entry ledger as the financial source of truth

**Status:** Confirmed principle; detailed D030 account catalog proposed for review

Record financial changes as immutable journal entries. Each journal must balance debits and credits independently for each asset. Derive balances from those entries. Correct mistakes with linked new entries rather than edits or deletions.

**Boundary / consequence:** Posting, corrections and account ownership need deliberate design. Balanced journals alone do not prove correct prices, actual cash or physical backing.

[Defense and planned evidence](defense/D006-double-entry-ledger.md)

### D007: Maintain balances synchronously in the ledger transaction

**Status:** Working proposal supporting the agreed ledger model

Keep AccountBalances in PostgreSQL and update it in the same transaction as the journal entries. Use the protected current balance for funding checks and rebuild it from entries when needed.

**Boundary / consequence:** Every posting updates balance rows, creating intentional contention on shared accounts. Reports may later use asynchronous projections.

[Defense and planned evidence](defense/D007-transactional-balance-projection.md)

### D008: Complete customer trades locally and procure pooled gold asynchronously

**Status:** Confirmed policy; procurement aggregation remains proposed

Keep pooled gold at the custodian and individual customer claims internally. Complete an accepted customer buy immediately. Process platform procurement separately, using existing stock and bounded uncovered exposure.

**Boundary / consequence:** The platform owns inventory, external-execution and price-movement exposure. Daily settlement, order execution and report timing must be distinguished.

[Defense and planned evidence](defense/D008-pooled-custody-and-immediate-trades.md)

### D009: Bound uncovered gold by 1% of total confirmed custody

**Status:** Confirmed outage policy; D032 healthy-mode/reservation defaults proposed

Let C be total confirmed pooled custody in mg, U total customer gold claims, and L = floor(C / 100). During custodian unavailability, accept a buy of q only when max(0, U + q - C) <= L. The 1% denominator is total confirmed custody, not free platform stock.

**Boundary / consequence:** This deliberately permits a short physical position. It is a business assumption, not a claim of regulatory approval. In-flight procurement does not count as confirmed custody.

[Defense and planned evidence](defense/D009-one-percent-backing-limit.md)

### D010: Display an advisory maximum and enforce capacity again at confirmation

**Status:** Confirmed

Show the lowest of platform backing capacity, wallet-affordable gold including fee and the configured order cap converted to gold at the buy price. Treat that number as advisory and recheck all applicable limits at confirmation.

**Boundary / consequence:** The maximum can change between display and confirmation. V1 rejects an oversized request rather than silently resizing it.

[Defense and planned evidence](defense/D010-display-maximum-buy.md)

### D011: Accept a gold sale only when its net proceeds can be immediately withdrawable

**Status:** Confirmed

Credit sale proceeds net of fee to the spendable rial wallet immediately. Reject a new sale when available platform liquidity cannot cover that obligation. Do not accept it into a delayed-cash state.

**Boundary / consequence:** Sales can be temporarily unavailable. Liquidity is confirmed usable cash minus distinct existing obligations and earmarks, not just the bank account balance.

[Defense and planned evidence](defense/D011-liquidity-gated-sales.md)

### D012: Replenish liquidity through operations, with no automatic custody sale

**Status:** Confirmed

Detect low liquidity, create a TreasuryRequest and expose it to operations. Operations arranges funding or a sale of free platform gold. Increase usable liquidity only after a confirmed financial inflow.

**Boundary / consequence:** Operations response time affects sale availability. A threshold should trigger action before liquidity is exhausted; its exact value is not yet set.

[Defense and planned evidence](defense/D012-manual-treasury.md)

### D013: Earlier unresolved top-up review policy

**Status:** Historical 20-minute rule superseded by D036. The requirement to account for genuine verified capture once is retained.

The previous design used a 20-minute operations review threshold. D036 now distinguishes callback arrival, the 15-minute mock verification window and Ledger delivery of verified capture evidence.

**Boundary / consequence:** An ambiguous response remains unresolved. Definitive mock verify expiry produces Failed without wallet credit. A verified capture awaiting Ledger still recovers under the original identity.

[Defense and planned evidence](defense/D013-payment-review-and-late-success.md)

### D014: Deduplicate both API requests and underlying financial operations

**Status:** Retained requirement; response contract refined by D027

Scope the public Idempotency-Key by customer and operation and compare a canonical fingerprint. Store one durable Application order, permanent Ledger OperationId and canonical 202 admission response. Ledger independently deduplicates producer operations and financial business references.

**Boundary / consequence:** Concurrent same-key requests replay the same admission response, not a changing order-status response. Financial finality is obtained through status and the durable Ledger outcome. Transport timeouts remain possible. Public key retention must not remove financial-operation deduplication.

[Defense and planned evidence](defense/D014-idempotency-and-business-deduplication.md)

### D015: Keep financial effects atomic inside Ledger and recover Application status separately

**Status:** Financial invariant retained; earlier cross-module transaction superseded by D026-D027

In one Ledger transaction, protect operation identity, financial eligibility, balances and shared controls; post all financial legs and persist the terminal result. Application separately commits intent before the call and result application afterward.

**Boundary / consequence:** The earlier proposal included quote/order/idempotency and ledger entries in one transaction. That is superseded. Application claims the quote and saves intent first; Ledger validates the immutable financial authorization, quote expiry and single use at acceptance. Application status can lag and requires durable recovery.

[Defense and planned evidence](defense/D015-atomic-local-posting-and-locks.md)

### D016: Persist dispatch, recovery and external-work intent in the owning service

**Status:** Working mechanism updated for D026-D027

Commit Application admission with its immutable Ledger request and recovery identity. Commit Application result application with procurement outbox work. Ledger stores its financial operation result with the journal. V1 recovery queries known OperationIds over HTTP; event publishing is optional, not a required second delivery path.

**Boundary / consequence:** The two services have separate commits. Workers need durable claims, retries and backoff. If integration events are introduced, the producer needs an outbox and consumers need deduplication. None of that replaces custodian-side request inquiry/idempotency.

[Defense and planned evidence](defense/D016-durable-outbox-and-workers.md)

### D017: Keep customer trades, top-ups, custody operations and treasury requests separate

**Status:** Confirmed separation; exact state names proposed

A completed customer trade is independent of its platform procurement state. A top-up is independent of a buy. Custody and payment ambiguity remain visible to support. A TreasuryRequest records operations work without changing assets by itself.

**Boundary / consequence:** Support needs correlation across several records. Identifiers and event history must be designed deliberately.

[Defense and planned evidence](defense/D017-separate-operation-lifecycles.md)

### D018: Use C# on .NET 10 and PostgreSQL

**Status:** Confirmed stack; EF Core 10 and Npgsql 10 proposed

Use C# and .NET 10 for the backend and PostgreSQL for persistence. The proposed access stack is ASP.NET Core 10, EF Core 10 and Npgsql 10.

**Boundary / consequence:** Exact database and package patch versions, deployment image and migrations need pinning during implementation.

[Defense and planned evidence](defense/D018-dotnet-postgresql-stack.md)

### D019: Use a modular monolith and one transactional database for V1

**Status:** Superseded historical proposal; replaced by D026

Historical proposal: use one modular application and one transactional database. The user subsequently chose an independent Ledger API alongside a modular Application API. D026 is the current deployment decision.

**Boundary / consequence:** Do not implement the historical single-context/single-deployment boundary. The selected architecture pays for one service boundary with durable operation recovery and restricts V1 to two hosts.

[Defense and planned evidence](defense/D019-modular-monolith.md)

### D020: Explicitly adapt callback acceptance scenarios to independent wallet top-up

**Status:** Confirmed deliberate deviation from literal brief wording

Test duplicate callbacks as one rial wallet credit, not direct gold credit. Test late and missing callbacks against the top-up lifecycle. Test quote expiry separately against customer trade confirmation.

**Boundary / consequence:** These are not literally identical to the original callback-to-gold scenarios. An evaluator may enforce the original wording despite the documented interpretation.

[Defense and planned evidence](defense/D020-adapted-callback-acceptance-tests.md)

### D021: Retain bidirectional reconciliation and traceable operation history

**Status:** Retained requirement; detailed workflow remains proposed

Compare external records with ledger-linked operations in both directions. Preserve event history and correlate payments, trades, journals and custody work. Distinguish report time, transaction time and settlement time.

**Boundary / consequence:** D036 confirms daily custody CSV and daily business-day IRR settlement. DESIGN section 13.1 proposes the mock schema. Calendar/cutoffs, tolerances, escalation ownership and closure evidence remain design work.

[Defense and planned evidence](defense/D021-reconciliation-and-history.md)

### D022: Use exact asset units and a small journal schema

**Status:** Working proposal

Represent IRR and GOLD_MG amounts as C# long and PostgreSQL bigint, with decimal/numeric for rates and intermediate arithmetic. Use LedgerAccounts, JournalTransactions, JournalEntries and AccountBalances as the initial schema vocabulary.

**Boundary / consequence:** Use checked range handling and sufficiently wide database aggregates. FINANCIAL_OPERATIONS and CHART_OF_ACCOUNTS now specify draft principal rounding, account classifications and controlled posting under D030-D033. Their approval and actual implementation remain work, not missing contract definitions.

[Defense and planned evidence](defense/D022-exact-units-and-posting-model.md)

### D023: Separate Customer, Identity and ledger ownership

**Status:** Confirmed separation; entity fields and identity mechanism proposed

Customer is the business owner of financial accounts and operations. Identity authenticates a principal. Ledger records the customer's financial claims. Customer has no RialBalance or GoldBalance fields. Link orders, payments, ledger accounts and bank-account destinations through CustomerId. Resolve the acting CustomerId from the authenticated principal and its trusted mapping.

**Boundary / consequence:** The proposed Customer fields are Id, IdentityUserId, Status, VerificationStatus and CreatedAtUtc. Exact identity storage, verification evidence and bank-account rules remain open. Staff identities need not have a Customer record. Ledger account labels UserRial and UserGold still refer to customer claims; they are not profile properties.

[Defense and planned evidence](defense/D023-customer-identity-and-ledger-ownership.md)

### D024: Require Active and Verified status for new customer financial instructions

**Status:** Confirmed policy; coordination mechanism proposed

Only a Customer with Status = Active and VerificationStatus = Verified may initiate a new top-up, buy, sell or withdrawal. Check eligibility at acceptance; an older valid quote does not authorize a newly ineligible customer. Continue processing genuine results of previously initiated operations.

**Boundary / consequence:** Verification and lifecycle state require an authoritative owner. Concurrent status changes must be coordinated with local financial acceptance. This decision does not select a verification provider, claim regulatory sufficiency, or define exactly how funds received after suspension become restricted. Suspension does not delete assets or automatically reverse completed trades.

[Defense and planned evidence](defense/D024-active-verified-financial-operations.md)

### D025: Use three backend projects with business modules inside Core

**Status:** Superseded historical folder proposal; replaced by D026 and revised PROJECT_STRUCTURE

Historical proposal: three backend projects with Ledger split across shared Core and Infrastructure. The selected independent Ledger now has its own API, Core, Infrastructure and versioned Contracts alongside Application projects. PROJECT_STRUCTURE is the current layout.

**Boundary / consequence:** Keep this record to explain the alternative, not as the active folder/DbContext plan. The current design has two deployable hosts and separately owned database contexts, without cross-service transaction sharing.

[Defense and planned evidence](defense/D025-project-layout-and-dependencies.md)

### D026: Deploy Ledger independently as the shared financial capability

**Status:** Confirmed direction; protocol and project details are working design

Use two backend deployments: a modular Application API and an independent Ledger API with a separately owned PostgreSQL database. Gold, future Fund and future Card use the same authoritative financial capability.

**Boundary / consequence:** There is one additional backend host, database boundary, internal API and recovery workflow. Ledger downtime stops new financial execution. One PostgreSQL instance can host both databases for the exercise, so engine-level failure isolation is not claimed.

[Defense and planned evidence](defense/D026-independent-ledger-service.md)

### D027: Use one atomic financial operation with durable inquiry and forward recovery

**Status:** Confirmed approach; exact API and state names are working design

Keep all financial legs, balances, controls and the terminal operation result in one Ledger transaction. Application durably records intent before calling Ledger and applies the known result afterward. Recover Unknown outcomes with the same OperationId; do not reverse a posted trade because Application status persistence failed.

**Boundary / consequence:** Application has transient Processing/Unknown states, durable dispatch and result-application records, and a recovery worker. The proposed public POST returns a stable 202 admission response; financial success is obtained from order status. Future multi-service business commitments may still need Saga-style orchestration.

[Defense and planned evidence](defense/D027-atomic-operation-and-forward-recovery.md)

### D028: Enforce spending, backing, liquidity and effective financial restrictions inside Ledger

**Status:** Working authority design preserving confirmed policies

Place the authoritative state used to accept a financial operation inside Ledger: balances, gold-backing facts/counters, liquidity commitments and effective financial eligibility. Application modules choose policy and verify external facts, then submit authorized versioned/deduplicated updates.

**Boundary / consequence:** Ledger includes financial authorization controls as well as bookkeeping. This is intentional and should remain narrower than product pricing or provider integration. CHART_OF_ACCOUNTS and FINANCIAL_OPERATIONS now define exact draft liquidity/reservation arithmetic and healthy-mode defaults. The new choices require review.

[Defense and planned evidence](defense/D028-ledger-financial-control-authority.md)

### D029: Bound the service split to the available team and assignment time

**Status:** Delivery proposal under the confirmed service direction

Build at most the Application API and Ledger API deployments for baseline V1, with one repository/stack, owned databases, hosted recovery workers and no required broker or Saga engine. Keep Fund/Card design-only and treasury manual.

**Boundary / consequence:** The lead remains a backend bottleneck. Actual build time and latency are unknown. The scope is a prioritization proposal, not proof the complete brief fits or a replacement for the required 12-week plan.

[Defense and planned evidence](defense/D029-bounded-two-service-delivery.md)

### D030: Define typed account templates and separate claims from real backing

**Status:** Detailed account catalog proposed for review

Adopt the explicit CHART_OF_ACCOUNTS catalog with asset, owner scope, normal side and balance rules for every baseline account. Customer claims are liabilities; confirmed bank/custody assets, noncash receivables, suspense and conversion controls remain distinct.

**Boundary / consequence:** This is an operational multi-asset ledger, not statutory reporting approval. Suspense cannot create cash/backing. Future fund unit scales and product economics still require activation contracts.

[Defense and planned evidence](defense/D030-explicit-chart-of-accounts.md)

### D031: Specify typed financial and control operations with replay-safe outcomes

**Status:** Detailed baseline contract proposed under the confirmed Ledger service direction

Use the versioned FINANCIAL_OPERATIONS contract for baseline financial templates and no-journal controls. Define payloads, posting patterns, guards, holds, durable outcomes, business/external action identities and recovery behavior instead of accepting raw journal lines.

**Boundary / consequence:** The contract is broader than the first implementation slice. Unsupported commands must be rejected explicitly, not simulated as Posted. Provider evidence is verified in Application and must remain traceable; no real gateway contract is invented.

[Defense and planned evidence](defense/D031-typed-financial-operation-contracts.md)

### D032: Make numeric and capacity defaults explicit for review

**Status:** Author-proposed defaults; prior confirmed policies retained

Specify integer IRR-per-gram pricing, whole-mg quantity, buy-principal ceiling, sell-principal floor, fee ceiling with ppm rates, exact cash obligations and conservative outgoing-gold capacity. D036 confirms the rates and bounds. Checking both limits on final pre-fee value remains a design interpretation.

**Boundary / consequence:** Healthy-mode 1%, the outgoing reservation denominator, restricted incoming-funds handling, demo buffer/threshold and correction controls remain proposals. D036 confirms withdrawal routing, while manual approval and hold timing remain design work.

[Defense and planned evidence](defense/D032-exact-rounding-and-financial-guard-defaults.md)

### D033: Restrict journal writes and verify projections inside the Ledger transaction

**Status:** Database enforcement and lock strategy proposed

Use a controlled post_journal boundary with restricted journal/projection DML, complete-journal per-asset validation, aggregate account updates and a deferred database validation. Couple journals, operation outcomes, evidence claims and controls in one Ledger transaction.

**Boundary / consequence:** The stored function/triggers are proposed, not implemented. V1 serializes short financial decisions on a shared control row, then locks related state/accounts deterministically. Measure contention; do not promise throughput without a test.

[Defense and planned evidence](defense/D033-controlled-journal-posting-and-projections.md)

### D034: Model Fund allocation and delayed cash as explicit pending claims

**Status:** Future product design proposal; V1 implementation excluded

Design Fund subscription/redemption with customer reservations, brokerage evidence, per-fill identities and explicit nonwithdrawable pending claims. Application owns business-calendar/cutoff and external recovery; Ledger owns units/cash. No Fund code is included in V1.

**Boundary / consequence:** Brokerage precision, fees, cancellation finality and calendar must be confirmed before activation. Proposed commercial limits are review choices, not existing agreements.

[Defense](defense/D034-fund-lifecycle.md)

### D035: Authorize Card spending through atomic funded holds and separate bank finality

**Status:** Future product design proposal; V1 implementation excluded

Design Card authorization, liquidation, capture, release, reversal and settlement on shared Ledger controls. Use IRR first, then consented gold; Fund fallback remains disabled without a funded ownership-transfer arrangement. No Card implementation is included in V1.

**Boundary / consequence:** Partner response deadlines, late presentment and cancellation finality are activation contracts. Independent services require durable unknown-outcome recovery; no offline spending or free bridge credit is assumed.

[Defense](defense/D035-card-authorization-and-settlement.md)

### D036: Apply company-confirmed V1 product rules

**Status:** Company-confirmed Q1–Q17, supplied 2026-11-07.

Apply the configured IRR order limits, paired prices, 30-second server-issued quotes, and 1% buy and 0.5% sell fees. Separate the mock verification deadline from callback arrival and Ledger credit delivery. Route above-ceiling withdrawals to Finance with verified Sheba ownership. Reconcile daily custody CSV and business-day IRR settlement separately.

[Full company answers and design interpretation](COMPANY_CLARIFICATIONS.md) | [Defense](defense/D036-company-confirmed-v1-rules.md)

## Changes to earlier assumptions and wording

1. D036 supersedes the earlier 20-minute top-up review rule. The 15-minute mock verify window and definitive provider status determine failed verification. Already verified capture can still reach Ledger later and post once.
2. Wallet-only purchases remain unchanged. Callback-to-gold acceptance scenarios are deliberately adapted, rather than adding payment-funded purchases or automatic buy resumption.
3. The company added a configurable 5,000,000,000 IRR per-order cap. Wallet funding, aggregate gold backing and liquidity constraints also apply.
4. The 1% allowance uses total confirmed pooled custody. It is not 1% of free stock and not a per-customer allowance.
5. Fees round upward to a whole IRR. Gold weight rounds downward to whole mg. Principal rounding is still open.
6. Pooled custody, daily CSV and daily business-day IRR settlement do not determine execution timing by themselves. Confirm that contract before finalizing procurement.
7. The application can store aggregate balances, provided they remain a rebuildable, transactionally updated projection of immutable entries.
8. UserId now explicitly refers to CustomerId. Customer eligibility, authentication and ledger ownership are separate responsibilities. New financial operations require Active and Verified status; prior genuine payment results still need accounting.

9. D026 supersedes the single-deployment proposal D019. D025 is historical; PROJECT_STRUCTURE now shows Application and Ledger hosts with owned databases.
10. D015's former all-in-one Application/order/ledger commit is superseded. Only financial effects and operation outcome share the Ledger transaction; Application admission/result application recover separately.
11. D014 now specifies an identical stored 202 admission response, with financial success obtained from order status. This refines the earlier same-final-response wording.
12. Effective financial restrictions and atomic backing/liquidity enforcement move to Ledger under the working D028 protocol. Application owns profile/policy intent and verified external evidence.
13. The time/team defense explicitly counts one backend-capable lead, one frontend engineer and a designer; it does not assume three backend engineers.

14. D030-D033 replace the former incomplete account/API outline with full proposed baseline definitions and numerical fixtures. Buy/sell examples were scaled to satisfy the source minimum rather than using a 490,000 IRR sale.
15. New author defaults explicitly cover principal rounding, ppm rates, healthy-mode exposure, outgoing reservations, cash obligations, restricted incoming funds and correction controls. They are not retroactively marked user-confirmed.

## Open details after the company answers

[Company clarifications](COMPANY_CLARIFICATIONS.md) records confirmed answers and their design interpretations. Open details include provider evidence and timing, manual Finance approval and hold timing, mock CSV review and cutoff calendar, healthy-mode backing, and exact arithmetic choices listed in FINANCIAL_OPERATIONS.

## Approval and implementation boundary

The request was to document the design and defense. This log does not approve unresolved choices, assert the whole brief is covered, or authorize external messages/publication. No tests have been executed; evidence in defense files is planned evidence.
