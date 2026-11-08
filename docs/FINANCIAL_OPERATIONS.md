# Avand Ledger - Financial Operations Contract

Date: 2026-11-06
Version: 1.0 draft
Status: Complete proposed baseline contract for design review, not implemented APIs. Explicitly confirmed policies remain confirmed; new defaults and recovery details below are author proposals requiring review.

[Chart of accounts](CHART_OF_ACCOUNTS.md) | [Ledger service](LEDGER_SERVICE.md) | [Posting examples](LEDGER_POSTING_EXAMPLES.md) | [Decisions](DECISIONS.md)

## 1. Authority and scope

Only Ledger changes financial accounts, balances, financial-control state, operation outcomes and reservations. Application verifies customer intent, commercial quotes and provider evidence, then sends typed requests over the authenticated internal API.

No caller submits an arbitrary journal or account ownership mapping. Asset/account selection follows a supported operation template. Provider calls never run inside a Ledger transaction.

This contract covers top-up/capture/settlement, buy/sell, withdrawal, custody procurement, manual treasury, suspense and correction. Refund/reversal templates cover separate verified post-capture events. For an unverified card payment, the issuer owns the return and Avand handles inquiry and messaging. Fund/Card extension contracts are specified separately in section 12.

## 2. Confirmed policy and draft calculation rules

See [Company clarifications](COMPANY_CLARIFICATIONS.md) for confirmed commercial policy. Exact arithmetic encoding, the order-limit check basis and other explicitly labeled design defaults remain design choices.

| Topic | Baseline value or rule |
| --- | --- |
| Gold price unit | Positive integer IRR per gram of the configured contractual gold grade |
| Buy principal | ceil(quantityMg * priceIrrPerGram / 1000) |
| Sell gross principal | floor(quantityMg * priceIrrPerGram / 1000) |
| Fee precision | Integer parts per million; 1% = 10,000 ppm |
| Fee | ceil(finalPrincipalIrr * feeRatePpm / 1,000,000); zero rate yields zero fee |
| Fee rates | Configurable buy 10,000 ppm (1%) and sell 5,000 ppm (0.5%) initially. No separate V1 fee floor or cap |
| Fee configuration validation | Proposed supported range is 0 <= feeRatePpm < 1,000,000. A sell must produce positive net proceeds |
| Gold order bounds | Configurable inclusive 1,000,000–5,000,000,000 IRR for buy and sell. Design interpretation applies the range to final pre-fee principal or gross value after conversion |
| Paired prices | One provider observation supplies the buy ask and sell bid together. Quote the selected side. Spread is separate from fee |
| Healthy-mode uncovered gold | Same 1% ceiling as outage mode, pending review; outage policy itself was confirmed |
| Local-demo cash buffer | Zero; production buffer must be configured by operations/finance |
| Local-demo low-liquidity alert threshold | 2,000,000 IRR; an explicit test setting, not production sizing |
| Withdrawal routing | Minimum 1,000,000 IRR. Ordinary ceiling 2,000,000,000 IRR. Above the ceiling, require manual Finance approval and payment |
| Withdrawal customer fee | Zero in the baseline mock; platform provider-fee cap is reserved separately |
| Incoming funds after restriction | Credit CustomerRialRestricted; later release is explicit, not automatic on profile activation |
| Corrections | Bounded reversal/replacement with distinct maker/checker; no forced negative customer balance |

For rial-input buy budget B, q = floor(B * 1000 / askIrrPerGram). Quote the actual principal after rounding q; fee is added separately. The whole-rial buy principal ceiling still fits the integer input budget. A user may need a larger wallet to cover the fee.

For gold-input orders, quantity is already a positive whole mg. For rial-input sales, the entered IRR is a gross gold-value target before fee. Compute q = floor(targetIrr * 1000 / bidIrrPerGram), gross = floor(q * bidIrrPerGram / 1000), fee = ceil(gross * feeRatePpm / 1,000,000), and net = gross - fee. Validate both limits against gross and require positive net. Show all final values before confirmation. For target 4,900,001 IRR at 9,800,000 IRR/g, q is 500 mg, gross 4,900,000, fee at 0.5% is 24,500 and net is 4,875,500 IRR. Apply the same order-value bounds to actual buy principal after conversion. Any change to quoted economics requires a new confirmation.

Use checked exact arithmetic with BigInteger intermediates where multiplication can exceed Int64; final asset amounts fit positive Int64. Do not use float/double or assume multiplying two Int64 values safely fits decimal. Quote calculations and Ledger validation use the identical formula.

A valid quote requires IssuedAt <= serverNow < ValidUntil, with ValidUntil exactly 30 seconds after server IssuedAt. First execution checks time after locks. If expired, reject and require a new quote and customer confirmation. Replay an existing identical terminal result before current expiry/restriction checks. Feed freshness controls issuance separately under the applicable policy.

## 3. Transport and envelope

Endpoints:
- POST /internal/v1/operations
- GET /internal/v1/operations/{operationId}
- POST /internal/v1/control-commands
- GET /internal/v1/control-commands/{commandId}
- GET /internal/v1/customers/{customerId}/balances
- GET /internal/v1/customers/{customerId}/financial-state
- GET /internal/v1/payments/{paymentId}/financial-state
- GET /internal/v1/holds/{holdId}
- GET /internal/v1/journals/{journalId}

All reads are authorized to the producer/operations role; an ordinary customer cannot call Ledger directly. ProducerId comes from validated service credentials and an allowed-operation matrix, not from trusting a body field.

customerId is required for customer-owned operations and omitted for platform-only movement templates. Never invent a customer ID for capital funding or bank/custody accounting.

Common financial envelope:
```json
{
  "schemaVersion": 1,
  "operationId": "14aa9c46-975b-4dd6-a2d8-cd53ea0f5848",
  "operationType": "BuyGold",
  "businessReference": {
    "kind": "GoldOrder",
    "id": "5d9fae75-6589-4140-afc2-6e42136005a7",
    "action": "Execute"
  },
  "customerId": "ebde3c90-d8ec-4f3c-a184-95362cc11864",
  "actor": { "kind": "Customer", "id": "ebde3c90-d8ec-4f3c-a184-95362cc11864" },
  "correlationId": "2d306d61-de11-40de-b086-c0cf713d46cd",
  "payload": {
    "quoteSnapshot": {
    "side": "Buy",
    "quoteId": "3c41b6f6-73b7-47b2-963b-dc82c25de491",
    "quoteIssuer": "ApplicationApi",
    "issuedAtUtc": "2026-11-06T08:00:00Z",
    "validUntilUtc": "2026-11-06T08:00:30Z",
    "priceIrrPerGram": "10000000",
    "quantityMg": "1000",
    "principalIrr": "10000000",
    "feeRatePpm": 10000,
    "feeIrr": "100000",
    "totalDebitIrr": "10100000",
    "quotePolicyVersion": 1
    }
  }
}
```

Amounts use canonical base-10 integer strings without separators, signs or leading zeros, except an explicitly signed bank-net field. Nonzero financial amounts are positive; optional zero fee is allowed but produces no zero journal line. Ppm is an integer. UUIDs use standard lowercase hyphenated form; times use normalized UTC. This avoids JavaScript precision loss for Int64 money.

Commercial quote-policy versions are immutable Ledger-registered records of both side-specific fee rates, minimum and maximum order value, and unit/rounding rules. Application activates a new issuing version only after its registration is acknowledged. Retain earlier versions for already-issued quotes and historical replay. An unknown/unregistered version is UnsupportedQuotePolicy.

quotePolicyVersion describes the commercial snapshot, not permission to bypass a newly effective financial risk limit. Ledger validates snapshot arithmetic and supported policy identity; it does not fetch a new market price. New funding/backing/eligibility restrictions apply at financial acceptance.

Canonical request hash:
- Deserialize a versioned typed schema, validate and normalize it, then serialize its declared fields deterministically and hash with SHA-256.
- Include operation type, ownership, business reference, actor, economic payload, evidence/approval references and quote snapshot.
- Exclude only transport delivery headers/tokens, trace attempt IDs and correlationId; these cannot change economics.
- Store HashVersion alongside the hash. Do not hash incidental JSON property order or whitespace.
- Preserve immutable request data in Application; do not recompute amounts on retry.

## 4. Results, errors and retry semantics

A valid financial decision returns HTTP 200:
```json
{
  "operationId": "14aa9c46-975b-4dd6-a2d8-cd53ea0f5848",
  "outcome": "Posted",
  "reasonCode": null,
  "journalIds": ["9fd5520e-b389-4257-9b83-98406e63dd95"],
  "ledgerSequence": "42",
  "decidedAtUtc": "2026-11-06T08:00:02Z",
  "businessReference": {
    "kind": "GoldOrder",
    "id": "5d9fae75-6589-4140-afc2-6e42136005a7",
    "action": "Execute"
  },
  "relatedOperationIds": []
}
```

Rejected uses the same envelope with no new journals and a stable reasonCode. Save the terminal result in the same transaction as the decision. Replays return the stored financial result; do not regenerate timestamps or current balances in it. Balances are read separately with an as-of sequence.

Control commands return Applied or Rejected with commandId, target/version where relevant and a durable decision. No-journal control changes must not pretend to be financial money postings.

| Condition | Response/meaning |
| --- | --- |
| Stored identical operation | HTTP 200 with exact stored Posted/Rejected result |
| Same operation identity, changed canonical content | HTTP 409 OperationPayloadConflict; original result unchanged |
| Different OperationId, same protected business action | Durable Rejected BusinessActionAlreadyRecorded with original operation reference; follow the original result |
| New request whose external evidence is entirely already applied | Durable Rejected EvidenceAlreadyApplied with related operation references; do not mark the original payment failed |
| Invalid schema/unknown type/out-of-range value | HTTP 400 before execution; no new financial effect |
| Invalid authentication/producer permission | HTTP 401/403; says nothing about an earlier attempt's financial outcome |
| Lock timeout, transient database failure, service unavailable | HTTP 503/timeout; caller keeps the outcome unresolved and retries/inquires with the same identity |
| GET returns NotFound | HTTP 404; an operation may still be uncommitted/in flight; not proof of definitive failure |
| Posted journal cannot be applied to Application status | Repair Application forward; no automatic financial reversal |

Proposed business rejection codes include CustomerNotEligible, CustomerRiskFrozen, QuoteExpired, QuoteAlreadyUsed, QuoteOwnerMismatch, InvalidQuoteArithmetic, BelowMinimumOrder, AboveMaximumOrder, InsufficientRialBalance, InsufficientGoldBalance, BackingCapacityExceeded, LiquidityUnavailable, UnreservedBankCashInsufficient, HoldNotReleasable, HoldAmountExceeded, EvidenceMismatch, AmountAlreadyRefunded, CorrectionNotPermitted, InsufficientBalanceForReversal, WithdrawalAmountOutOfBounds, WithdrawalApprovalRequired and UnsupportedQuotePolicy.

A business rejection does not retry into success after a top-up or policy change. A new customer decision needs a new quote/order. A retryable transport failure is not converted to business Rejected.

Producer operation identity is (AuthenticatedProducerId, OperationId). Business action uniqueness is (ProducerId, business kind, business id, action). Successful quote uniqueness is (trusted QuoteIssuer, QuoteId). External action uniqueness includes the provider namespace, merchant/account, action type and external action reference.

For concurrent duplicate operation claims, wait/read the winning committed result and compare its hash. An initial fast-path NotFound cannot bypass those constraints.

### 4.1. Producer and actor permissions

| Caller scope | Allowed operation families |
| --- | --- |
| Application.Trading | BuyGold, SellGold and matching balance/result reads |
| Application.Payments | Initiation authorization, verified capture/settlement, withdrawal/refund under pinned customer intent/evidence |
| Application.Custody | Approved procurement reservation/confirmed movements; no customer trading authority |
| Application.Treasury | Manual request and approved funding/free-gold reservations; no arbitrary balance editing |
| Finance.Adjustment | Evidenced suspense/correction and separately approved release; no ordinary customer impersonation |
| Support.Read | Authorized evidence and balances only |

These can be credentials/scopes within the same Application deployment, not additional services. A body actor ID is not authority: validate delegated staff/customer claims or Ledger approval records and bind them to the service scope. Maker/checker must be distinct authorized principals with immutable approval records for the case/template/evidence digest; two arbitrary IDs in JSON are insufficient.

## 5. Financial operation catalog

Fields below are payload fields in addition to the common envelope. Evidence references identify immutable verified Application-side evidence with an integrity digest and restricted retrieval path; Ledger does not trust a raw callback or user-provided screenshot.

### F01. ConfirmCapitalInflow

**Payload:** bankAccountRef, amountIrr, externalActionRef, evidenceRef.

**Journal template:** Debit BankCash; Credit PlatformCapital.

**Guards / authority:** Finance-scoped evidence of real owner funding. A customer top-up cannot use this template.

### F02. ApplyGatewayPaymentEvidence

**Payload:** paymentId, captureRef, capturedIrr, evidenceBatchId, confirmedReversals[], evidenceRef.

**Journal template:** New capture: Debit GatewayReceivable; Credit customer available/restricted claim. Apply known refunds/reversals as described below.

**Guards / authority:** Lock payment financial state. Capture/refund action identities and amounts must agree with previously recorded evidence. Never create the capture twice.

### F03. RecordGatewaySettlement

**Payload:** merchantRef, captureAllocations[], refundPayableAllocations[], feeIrr, signedBankNetIrr, bankActionRef, evidenceRef.

**Journal template:** Debit BankCash for positive net (credit for negative); Debit ProviderFeeExpense and GatewayRefundPayable offsets; Credit GatewayReceivable gross.

**Guards / authority:** Net = gross receivables - refund-payable offsets - fee. Match each allocation once; do not settle a refund-reserved receivable without the matching refund evidence.

### F04. BuyGold

**Payload:** quoteSnapshot containing side=Buy, quote identity/times, priceIrrPerGram, quantityMg, principalIrr, feeRatePpm, feeIrr, totalDebitIrr and quotePolicyVersion; customerId is only in the envelope.

**Journal template:** Debit CustomerRialAvailable total; Credit TradingRialClearing principal and FeeRevenue fee. Debit PlatformGoldPosition mg; Credit CustomerGoldAvailable mg.

**Guards / authority:** Effective Active/Verified, valid unused buy quote, exact arithmetic, both order-value bounds, wallet >= total, backing after buy within limit.

### F05. SellGold

**Payload:** quoteSnapshot containing side=Sell, quote identity/times, priceIrrPerGram, quantityMg, grossIrr, feeRatePpm, feeIrr, netCreditIrr and quotePolicyVersion; customerId is only in the envelope.

**Journal template:** Debit CustomerGoldAvailable mg; Credit PlatformGoldPosition mg. Debit TradingRialClearing gross; Credit customer IRR net and FeeRevenue fee.

**Guards / authority:** Effective Active/Verified, valid unused sell quote, exact arithmetic and both order-value bounds, available gold >= quantity, FreeLiquidity >= net. Posted net proceeds are immediately available for buying and withdrawal.

### F06. ReserveWithdrawal

**Payload:** customerId, withdrawalId, amountIrr, bankDestinationRef, ownershipEvidenceRef, maxProviderFeeIrr. The manual path additionally requires verifiable Finance approval bound to the request and destination. Its exact payload and hold timing remain to be specified.

**Journal template:** Debit CustomerRialAvailable; Credit WithdrawalPayable. Create one principal/fee bank reservation.

**Guards / authority:** Active/Verified; Sheba ownership verified against the customer's national ID; configured minimum and ordinary ceiling or authorized manual approval above the ceiling; wallet >= amount; unreserved bank cash >= amount + fee cap; free liquidity covers platform fee cap.

### F07. SettleWithdrawal

**Payload:** withdrawalHoldId, bankTransferActionRef, paidPrincipalIrr, actualFeeIrr, evidenceRef.

**Journal template:** Debit WithdrawalPayable principal and ProviderFeeExpense fee; Credit BankCash principal + fee.

**Guards / authority:** Verified bank payment for the pinned destination; principal/fee do not exceed remaining authorization. Consume matching reservation; allow evidenced partial settlement without releasing the unknown remainder.

### F08. ReleaseWithdrawal

**Payload:** withdrawalHoldId, failureOrCancellationEvidenceRef.

**Journal template:** Debit remaining WithdrawalPayable; Credit customer IRR available/restricted. Release only the remaining matching cash reservation.

**Guards / authority:** Allowed before dispatch or after definitive nonpayment/cancellation. Unknown is not releasable; do not release already-paid principal.

### F09. ReserveRefund

**Payload:** customerId, paymentId, refundId, amountIrr, sourceBucket, fundingRoute, maxProviderFeeIrr, evidenceRef.

**Journal template:** Debit the selected available/restricted customer IRR claim; Credit RefundPayable. Reserve the original payment refund entitlement and its funding route.

**Guards / authority:** Captured amount minus confirmed/active refunds covers the request. Source claim exists. Bank route needs physical reservation; gateway route needs an unmatched, unreserved receivable from this payment. Restricted-source recovery requires operations authority.

### F10. SettleRefund

**Payload:** refundHoldId, providerRefundActionRef, amountIrr, settlementKind, actualFeeIrr, evidenceRef.

**Journal template:** Debit RefundPayable; Credit GatewayReceivable, BankCash or GatewayRefundPayable according to verified funding evidence. Record evidenced platform fee separately in the same journal.

**Guards / authority:** Return is to the original payment route. Consume customer refund entitlement once. If merchant debit is pending, retain/convert its cash reservation against GatewayRefundPayable.

### F11. ReleaseRefund

**Payload:** refundHoldId, failureOrCancellationEvidenceRef.

**Journal template:** Debit remaining RefundPayable; Credit original customer available/restricted claim. Release remaining entitlement/funding reservation.

**Guards / authority:** Only definitive non-refund or safe cancellation before dispatch. Unknown provider execution remains held.

### F12. RecordGatewayRefundDebit

**Payload:** refundPayableAllocationRefs[], bankActionRef, amountIrr, feeIrr, evidenceRef.

**Journal template:** Debit GatewayRefundPayable and any ProviderFeeExpense; Credit BankCash.

**Guards / authority:** Match the already-confirmed customer refund and pending merchant debit. Consume the represented cash reservation; no second customer debit.

### F13. RecordCustodianCashPayment

**Payload:** procurementId, cashHoldId, paymentActionRef, principalIrr, feeIrr, mode, evidenceRef.

**Journal template:** Prepay: Debit CustodianAdvance; Credit BankCash. Payable: Debit CustodianPurchasePayable; Credit BankCash. Add actual provider fee if any.

**Guards / authority:** Real payment within remaining authorization. Consume the matching reservation. Paid advance is not gold and cannot increase C.

### F14. RecordCustodianGoldReceipt

**Payload:** procurementId, receiptActionRef, quantityMg, allocatedCostIrr, fundingMode, evidenceRef.

**Journal template:** Debit CustodianGold; Credit PlatformGoldPosition in mg. IRR: Debit TradingRialClearing; Credit CustodianAdvance or CustodianPurchasePayable.

**Guards / authority:** Confirmed receipt and cost allocation within accepted procurement. Prepaid allocation cannot exceed advance. A payable converts the matching cash commitment to a represented payable without double counting.

### F15. RecordCustodianGoldSale

**Payload:** treasuryRequestId, goldHoldId, fillActionRef, quantityMg, grossIrr, feeIrr, evidenceRef.

**Journal template:** Debit PlatformGoldPosition; Credit CustodianGold in mg. Debit CustodianCashReceivable gross; Credit TradingRialClearing gross. Debit fee expense; Credit that receivable for deducted fee.

**Guards / authority:** Manually initiated approved sale; confirmed physical disposal within held quantity. Consume hold as C falls. Receivable is not cash and does not replenish liquidity.

### F16. RecordCustodianCashReceipt

**Payload:** saleReceivableAllocations[], bankActionRef, grossReceivableIrr, newFeeIrr, bankNetIrr, evidenceRef.

**Journal template:** Debit BankCash net and any newly evidenced ProviderFeeExpense; Credit CustodianCashReceivable gross.

**Guards / authority:** Net = allocated receivable - new fee. A fee already recorded on the sale cannot be deducted again. Capacity increases only with confirmed bank cash.

### F17. RecordProviderFee

**Payload:** providerFeeActionRef, bankAccountRef, amountIrr, cashReservationRefOrCaseId, evidenceRef.

**Journal template:** Debit ProviderFeeExpense; Credit BankCash.

**Guards / authority:** Real fee not previously included in a settlement journal. Unexpected fee uses an evidenced finance case; it is not a customer fee.

### F18. RecordUnmatchedExternalMovement

**Payload:** caseId, movementActionRef, direction, asset, amount, sourceRef, evidenceRef.

**Journal template:** Cash in: BankCash / SuspenseCredit. Cash out: SuspenseDebit / BankCash. Gold in: CustodianGoldUnidentified / GoldSuspenseCredit. Gold out: GoldSuspenseDebit / CustodianGold.

**Guards / authority:** Record an evidenced unmatched physical movement under finance authority; it creates an incident, not free capacity. If books lack prior movements needed for a valid nonnegative physical balance, reconcile the missing statement evidence first.

### F19. ResolveSuspense

**Payload:** caseId, resolutionTemplate, amountOrQuantity, targetBusinessRef, approvalRefs, evidenceRef.

**Journal template:** Use only the bounded resolution templates in section 9.

**Guards / authority:** Resolve real ownership or proven loss; no arbitrary entry list from the caller. Maintain asset conservation and evidence links.

### F20. PostCorrection

**Payload:** caseId, originalJournalId, correctionTemplate, replacementBusinessData, makerRef, checkerRef, reasonCode, evidenceRef.

**Journal template:** Post a full linked reversal and optional typed replacement atomically; never mutate the original journal.

**Guards / authority:** Distinct authorized maker/checker, one full reversal per original, no negative protected account or invented physical cash/gold. If dependent spending prevents it, reject this template and keep the finance case restricted.

### F21. ReclassifyCustomerAssets

**Payload:** customerId, asset, amount, fromBucket, toBucket, caseId, approvalRefs.

**Journal template:** Debit source customer liability; Credit destination customer liability in the same asset.

**Guards / authority:** Only Available/Reserved/Restricted transitions supported by a named release/restriction case; generic movement cannot consume another active hold. Restricted-to-available needs effective eligibility and explicit release approval.


The API dispatches operationType to one template. It does not accept the text of these posting instructions as input.

## 6. Capture, failure, refund and reversal

Maintain per-payment financial state: captured amount/reference, amount already reversed/refunded, active refund reservations, receivable allocations and external-action history. Lock it with the operation. Customer return entitlement plus active entitlement reservations cannot exceed capture; actual excess returns and uncertain duplicate-outflow exposure are handled explicitly in section 15.

### No verified capture

A callback can arrive at any time. Use server-to-server verify/inquiry as authority. The mock rejects verification outside its 15-minute window. Record a definitive expired rejection as Failed with no wallet credit. A timeout or ambiguous response requires inquiry and remains unresolved. For an unverified card payment, the issuing bank handles the return within the company's stated 72-hour window. Avand records the gateway-confirmed status and displays the appropriate message. This issuer return creates no Avand wallet credit, cash debit or refund dispatch. Keep the evidence in Application's event history.

### New verified capture

Apply verified capture once: Debit GatewayReceivable; Credit CustomerRialAvailable for an eligible customer, otherwise apply the approved restriction policy (the draft uses CustomerRialRestricted). Verified evidence can be delivered late even though a new verification would now be expired. Gateway settlement is separate and a receivable is outside usable cash.

### Capture already refunded before Ledger sees it

Apply verified capture and verified returns in one atomic operation. For captured A and already-returned R:
- Debit GatewayReceivable A.
- Credit customer available/restricted A - R.
- Credit RefundPayable R.
- Debit RefundPayable R.
- Credit the evidenced refund source R.

If the full payment was refunded from unsettled gateway money, all net balances are zero and the wallet is never spendable. Claim both the capture and return action identities. A later callback cannot credit the capture again.

Mixed evidence batches may repeat an already-known capture while introducing a genuinely new return. Validate known facts without reposting them, apply only new external actions and update payment state atomically. Conflicting known amounts are EvidenceMismatch, not an excuse to rewrite history.

### Requested refund after wallet credit

Reserve a real customer claim and original payment refund entitlement before external dispatch. Pin the original return route. Funding is either that payment's unreserved GatewayReceivable or reserved BankCash.

On confirmed refund:
- Gateway receivable offset: Debit RefundPayable / Credit GatewayReceivable.
- Confirmed bank debit: Debit RefundPayable / Credit BankCash.
- Provider paid the customer but merchant debit is pending: Debit RefundPayable / Credit GatewayRefundPayable; keep/convert the bank reservation until merchant settlement.

Unknown refund execution stays reserved. A later capture callback is an already-applied fact, not another wallet credit.

### Involuntary confirmed reversal after spending

A real provider reversal is not rejectable merely because the customer spent funds. Debit remaining reclaimable customer available/restricted claims up to their balance; debit SuspenseDebit for the unrecovered remainder; credit the evidenced GatewayReceivable or cash/payable source. Do not seize another active payout hold or make the customer balance negative.

Open a finance case and Ledger risk freeze atomically. Suspense is not a legally asserted debt or usable liquidity. Finance later resolves it to a proven recovery or approved loss. Record a provider-paid-but-platform-debit-pending obligation through GatewayRefundPayable when appropriate.

This recovery path needs provider evidence and privileged operation authority; ordinary customer refund initiation cannot use it.

## 7. Reservation and external execution contract

Every hold has HoldId, business owner/reference, kind, asset, authorized principal/fee, remaining amounts, funding route, optional represented payable, pinned destination/external request, state and version.

States:
- Active: authorized, not yet potentially dispatched.
- DispatchStarted: external identity persisted before the network call.
- Unknown: external outcome unresolved; remaining funds stay reserved.
- PartiallyConsumed: a proven portion completed; remainder remains protected.
- Consumed: fully settled.
- Released: proven unused remainder released.
- ClosedPartial: part paid and remaining part definitively released.

Reject a withdrawal below the configured minimum. Route above-ceiling requests to manual Finance approval and payment. Block unapproved above-ceiling reserve or dispatch and preserve the request for review. The manual path uses the same destination ownership, financial authorization and verified settlement controls. Larger requests do not enter automatic dispatch or automatic splitting.

Mark dispatch intent before sending. A crash between that mark and actual send may require safe provider inquiry/retry; the system cannot assume it was never sent.

Capture and release serialize on the same hold. A winning final action prevents the other from consuming that amount. Never release from a different hold merely because aggregate reserves are sufficient.

No automatic release after elapsed time for potentially dispatched withdrawals, refunds, custody payments or gold outflows. Expiry is not proof of nonexecution. Release requires a safe pre-dispatch cancellation or provider-confirmed nonpayment/cancellation. Application delivery timeout does not release a Ledger reservation.

Partial verified settlement consumes only the proven principal/fee. Keep the rest held until a definitive result. Provider fills/cash payments use distinct immutable action references and remaining authorization checks.

For custody gold receipt before payment, convert the corresponding unrepresented cash commitment into a payable-backed reservation in the same transaction. Do not deduct both. For outgoing gold, a completed movement lowers C and consumes its G reservation atomically.

## 8. Control command catalog

These commands have commandId, business reference, actor, canonical payload hash and durable Applied/Rejected result. Application persists command intent with desired state; recovery uses the same identity/version. No financial journal is created unless a separate typed operation actually moves/reclassifies an account.

| ID | Type | Required payload | Effect |
| --- | --- | --- | --- |
| C01 | ProvisionCustomerAccounts | customerId, profileReference, commandId | Create deterministic unique account instances; no journal or initial money. |
| C02 | SetFinancialEligibility | customerId, desiredState, version, commandId | Serialize effective Active/Verified state. Older versions cannot override newer; same version with changed content conflicts. |
| C03 | SetFinancialPolicy | policyTarget, expectedVersion, newVersion, values, commandId | Apply validated control parameters; tightening below existing exposure freezes new risk, not past trades. |
| C04 | AuthorizeTopUpInitiation | customerId, paymentIntentId, commandId | Under the financial gate, record a one-time initiation authorization. This is not capture and creates no cash. Application creates the gateway session after durable authorization; later results remain accountable. |
| C05 | ReservePlatformCash | businessRef, amountIrr, maxFeeIrr, purpose, commandId | Reserve only free platform liquidity, also checking physical cash. A payable-backed reservation is explicitly linked, not deducted twice. |
| C06 | ReservePlatformGold | treasuryRequestId, quantityMg, commandId | Manual treasury sale only; reserve free gold using C - U - active outgoing holds. |
| C07 | MarkExternalDispatchStarted | holdId, externalRequestRef, commandId | Pin external identity before dispatch. A crash after this point is treated as potentially executed, even if the send outcome is unknown. |
| C08 | MarkExternalOutcomeUnknown | holdId, evidenceRef, commandId | Keep remaining reservation active and visible; time elapsed does not release it. |
| C09 | ReleasePlatformReservation | holdId, definiteFailureOrSafeCancellationRef, commandId | Release only provably unused remainder. Unknown and already-consumed amounts cannot be released. |
| C10 | SetRiskFreeze | customerOrGlobalScope, caseId, reason, commandId | A Ledger-local risk freeze blocks new risk-increasing instructions; processing true earlier financial results continues. |
| C11 | ApproveAdjustmentCase | caseId, makerRef, checkerRef, permittedTemplate, evidenceDigest, commandId | Authorize a bounded correction/resolution. An approval never changes balances itself. |

Effective eligibility is Active AND Verified AND not RiskFrozen. Application blocks new local initiation while a restriction is requested. Ledger serializes financial acceptance with its effective restriction; Application cannot report global application before the matching version is acknowledged/read back. Older acknowledgements never apply newer desired state.

New top-up initiation gets C04 authorization under this gate before the gateway session is created. Its later verified result is a historical financial fact and may credit Restricted after suspension. Successful authorization creates no funds.

C10 includes the desired frozen state and approval references. Setting a freeze may be automatic for a proven financial incident; clearing it requires the case's approved finance authority. C03 must reject uncovered-ratio values above the selected V1 hard ceiling of 10,000 ppm; raising that ceiling requires a new business decision, not merely a version number.

A risk freeze is separate from profile verification. A stale profile activation cannot clear it. Only an explicitly approved finance control command may release the corresponding freeze after case resolution.

## 9. Suspense resolution and manual correction

Allowed F19 templates:
- Unidentified cash belongs to a customer: Debit SuspenseCredit / Credit customer available/restricted.
- Unidentified cash settles an already-credited gateway capture: Debit SuspenseCredit / Credit GatewayReceivable; do not credit the customer twice.
- Confirmed owner funding: Debit SuspenseCredit / Credit PlatformCapital.
- Approved cash-loss write-off: Debit CorrectionLossExpense / Credit SuspenseDebit.
- Unidentified gold proven eligible: Debit GoldSuspenseCredit / Credit PlatformGoldPosition, and Debit CustodianGold / Credit CustodianGoldUnidentified.
- Approved physical shortage write-off: Debit PlatformGoldPosition / Credit GoldSuspenseDebit. No new physical gold appears.

F20 supports a proven full mistaken-journal reversal plus optional replacement by a supported business template. The reversal must be independently fundable before replacement; netting cannot pretend spent customer assets are available. A unique originalJournalId full-reversal claim prevents two full reversals. Both journals share one operation transaction. Reversal references are append-only.

Reject if current balances or active dependencies prevent the bounded template. Keep an adjustment case and required restriction; do not fabricate cash, physical gold or a collectible debt. F19 write-offs/reclassifications require their own approved evidence and cannot be used to hide unrelated errors.

Suggested V1 dual control uses two distinct seeded operations/finance identities; it is an author-proposed control, not a claim the initial engineering team has extra backend staff.

## 10. Local transaction and storage enforcement

Proposed V1 lock order for every financial writer:
1. Durable operation identity and ordered business/external-action claims.
2. Shared TreasuryAndBacking financial-control row.
3. Financial customer state rows ordered by CustomerId.
4. Related payment/procurement/hold/case rows ordered by stable type/id.
5. Account balance rows ordered by AccountId.

Control updates that touch these resources follow the same order. READ COMMITTED with these protected checks is the proposed baseline; a plain read then SaveChanges does not provide this contract.

The global financial-control row intentionally serializes the short V1 posting decisions. Measure contention; no throughput claim is made. It can later become narrower guarded resources while preserving the external operation contract. Never hold it during HTTP/provider calls.

Assign ledgerSequence inside this serialized decision boundary, not from a preallocated sequence assumed to equal commit order. Read balance state and its sequence from one database snapshot.

post_journal validates positive lines, account ownership/asset mapping, final nonnegative bounds and per-asset equality. It updates account projections from net account deltas once. A deferred constraint validation also rejects an unbalanced completed journal; row CHECK alone is insufficient.

Restrict journal/projection DML to the posting boundary; migrations use separate credentials. Keep terminal financial/control results, evidence claims and capacity/hold updates in the same transaction. Unexpected invariant/SQL failures roll back and remain technical failures; do not falsely persist business rejection after a partially failed write.

No SQL, stored function, migration or runtime test is claimed to exist yet.

## 11. Application coordination and operational finality

Application transaction A stores one request, quote claim, order, OperationId and canonical 202 acknowledgement. It does not claim financial success.

Application then sends/inquires the immutable request. Ledger Posted is the financial finality point. Application transaction B applies it once, stores journal references and creates procurement intent/outbox once. Recovery repairs Application state forward.

A result loss does not cancel a trade or generate a replacement identity. Replaying a Posted result after expiry/restriction is safe; it does not execute again.

Top-up has VerifiedPendingLedger while capture is verified but wallet credit remains unresolved. The 15-minute mock window concerns gateway verification, not Ledger delivery. Provider uncertainty may enter PendingReview under an operations policy. A definitive verify expiry is Failed, and verified pending Ledger delivery continues under its original identity.

Withdrawal/refund is final financially only when its proven settlement or safe release posts. Provider Unknown is not financial Failed.

## 12. Fund and Card extension contracts

Authoritative proposed product workflows are [Fund](FUND_PRODUCT_FLOW.md) and [Card](CARD_PRODUCT_FLOW.md). Their explicit fee/fill and bank-finality rules refine this earlier fee-neutral sketch. Neither adds V1 code scope.

These are ledger-side design hooks. They do not authorize implementing the two future products or invent commercial pricing/settlement guarantees.

### Fund subscription

1. Reserve customer funds: Debit CustomerRialAvailable / Credit CustomerFundSubscriptionPending; create a matching cash commitment so this reclassification does not create free treasury cash.
2. Pay brokerage: Debit BrokerSubscriptionAdvance / Credit BankCash; consume the cash commitment, leaving the nonwithdrawable pending claim.
3. Confirm units: IRR Debit CustomerFundSubscriptionPending / Credit BrokerSubscriptionAdvance; fund asset Debit BrokerageFundUnits / Credit CustomerFundAvailable.
4. Definitive cash return before allocation: Debit BankCash / Credit BrokerSubscriptionAdvance and Debit CustomerFundSubscriptionPending / Credit CustomerRialAvailable (or Restricted) in the same operation.
5. Before-dispatch cancellation releases the customer pending claim and cash commitment. Unknown brokerage submission remains pending.

Exact unit asset scale, allocation fees, cost differences and partial allocation require the fund product contract before activation.

### Fund redemption

1. Reserve units: Debit CustomerFundAvailable / Credit CustomerFundReserved.
2. Confirm redemption: Debit CustomerFundReserved / Credit BrokerageFundUnits; IRR Debit BrokerRedemptionReceivable / Credit CustomerFundRedemptionPending.
3. Receive cash: Debit BankCash / Credit BrokerRedemptionReceivable; Debit CustomerFundRedemptionPending / Credit CustomerRialAvailable (or Restricted).
4. Definitive rejection before units are redeemed releases reserved units; unknown outcome does not.

Pending redemption is not spendable wallet cash. The one-to-two-business-day source delay is represented explicitly.

### Card

1. Authorize: Debit CustomerRialAvailable / Credit CustomerRialReserved and reserve the required physical settlement cash.
2. Capture: Debit CustomerRialReserved / Credit CardSettlementPayable; retain the matching cash reservation.
3. Settle bank: Debit CardSettlementPayable / Credit BankCash; consume the cash reservation.
4. Release unused authorized amount only under the bank protocol: Debit CustomerRialReserved / Credit CustomerRialAvailable (or Restricted).
5. A quote-authorized gold sale plus IRR reservation can be one atomic Ledger operation if both the gold balance and funded liquidity cover it.

Do not turn a fund redemption receivable into immediate card cash. Without a funded bridging/credit arrangement, that fallback cannot be promised. Bank deadlines, late presentment, partial capture and release semantics must be specified before activation.

## 13. Delivery scope versus contract completeness

The contract defines the full baseline accounting vocabulary; it does not promise every operational screen/adapter/template fits the 72-hour implementation. Prioritize the retained automated gold/top-up/ledger scenarios, durable cross-service recovery and the required withdrawal path. Include explicit supported-operation capability/configuration in the implementation; an unimplemented type returns UnsupportedOperationType before execution, never a fake Posted result.

The numerical refund/correction/custody and exception templates also satisfy the design obligation even if advanced automation is left to operations in the first demo. Report actual omissions in README/AI/demo material. Fund/Card extension activation remains outside baseline code. Templates share one posting boundary; they are not separate services or a generic workflow platform.

## 14. Required evidence

Baseline acceptance scenarios plus:
- Exact whole-unit math, side-specific fee rates and both order-value boundaries.
- Mock verify expiry and issuer-return messaging, with late notification and already verified evidence tested separately.
- Withdrawal routing at and above the ordinary ceiling, Finance approval and national-ID/Sheba ownership mismatch.
- Daily custody CSV reconciliation and separate business-day cash settlement evidence.
- Replay after expiry, changed payload and different OperationId for the same business action.
- Journal/projection/operation-result atomicity and direct invalid-write protection.
- Concurrent spend, backing, liquidity and hold capture/release.
- Verified payment already returned before wallet delivery.
- Involuntary reversal after spending with suspense/risk freeze rather than negative wallet.
- Gateway settlement with fee and refund offsets, without double recognizing either.
- Partial settlement preserves remaining hold.
- Outgoing custody reservation versus a concurrent new buy.
- Unidentified receipts create no free cash/backing.
- Correction/reversal authority and inability to edit history.
- Control intent/acknowledgement recovery.

[Machine-readable posting fixtures](examples/ledger-postings.json) and their Markdown rendering are accounting-design checks. They do not replace real two-service/PostgreSQL tests.

## 15. Review clarifications: resource identity and reversal conflicts

### Returned hold identities

ReserveWithdrawal, ReserveRefund, ReservePlatformCash and ReservePlatformGold generate a HoldId inside their committing transaction. Their immutable financial/control result includes resources.createdHolds, each with holdId, kind, businessReference, asset, authorizedPrincipal, authorizedFee and remainingAtDecision. Exact replay returns that same creation snapshot, not current remaining amounts. GET hold returns live state/version and remaining authorizations. Non-reserve results use an empty createdHolds array. Application persists the returned identity before dispatch; a lost response is recovered through the original operation/control CommandId. No replacement hold is created on retry.

### Quote schema and time

BuyGold and SellGold payloads contain one nested quoteSnapshot, with no repeated economic fields outside it. Customer ownership is the envelope customerId, attested by the trusted Application issuer and bound to Ledger's quote claim. Reject unknown schema properties. Ledger uses zero future-issued clock allowance in the baseline mock: IssuedAt must not exceed the protected decision time. Production clock configuration must not silently extend the lock. Stored-result replay bypasses new time checks.

### Provider reversal overlapping a refund

Lock the payment state and all related refund holds before applying return evidence. A return matching a refund's verified external request/action is that hold's settlement, not a separate reversal.

For an independent reversal, first consume the overlapping customer RefundPayable claim, then remaining reclaimable Available/Restricted claims, with any shortage debited to SuspenseDebit. Credit the evidenced external asset/payable source once. Mark the consumed customer return entitlement satisfied. Do not also restore that refund amount to the wallet.

A provably undispatched overlapping refund is closed as SatisfiedByProviderReversal and its unused funding reservation is released atomically. For DispatchStarted or Unknown, stop further dispatch/re-execution and place the hold in OutcomeConflict. Preserve its possible external outflow until definitive inquiry/cancellation resolves it. An ordinary NotFound is not proof of cancellation.

Separate customerPayableRemaining from externalExposureRemaining. Once reversal consumes the customer payable, the uncertain outgoing amount becomes an unrepresented potential-loss commitment H, rather than remaining a second customer refund entitlement. Retain or establish the physical cash reservation even if this makes headroom negative; freeze new risk and alert operations instead of rejecting the real reversal.

If that uncertain refund later actually pays again, debit SuspenseDebit and credit BankCash, or GatewayRefundPayable if merchant debit is pending. Consume/convert the matching exposure reservation. Never debit the customer twice or automatically assert a collectible debt. If definitively cancelled, release only the exposure with C09; do not call a refund-release template that credits a customer claim already satisfied.

Track CustomerReturnSatisfied up to CapturedAmount, ActiveRefundEntitlement and separate ExcessActualReturn/UncertainOutflow exposure. The entitlement invariant is CustomerReturnSatisfied + ActiveRefundEntitlement <= CapturedAmount. Actual proven provider returns may exceed capture during an incident; record that excess as a finance case rather than rejecting financial truth or permitting more refunds.

Example: capture 2,000,000 IRR; refund payable 1,000,000; available wallet 1,000,000. An independent full reversal debits RefundPayable 1,000,000 and Available 1,000,000 and credits GatewayReceivable 2,000,000. Both customer claims become zero. If the refund was potentially dispatched, retain 1,000,000 exposure; a later duplicate bank payout debits SuspenseDebit and credits BankCash for that amount. Definitive cancellation releases the exposure without wallet credit.
