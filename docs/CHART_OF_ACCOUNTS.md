# Avand Ledger - Chart of Accounts

Date: 2026-11-06
Version: 1.0 draft
Status: Complete proposed baseline account catalog for review. Confirmed business policies are retained; newly specified accounting/control choices are author proposals, not asserted user approvals.

[Design](DESIGN.md) | [Ledger service](LEDGER_SERVICE.md) | [Financial operations](FINANCIAL_OPERATIONS.md) | [Posting examples](LEDGER_POSTING_EXAMPLES.md)

## 1. Scope and perspective

These are Avand's operational books, not the customer's personal books. Customer balances are liabilities owed by Avand. Physical custody and bank cash are assets. Each asset is balanced in its own units.

This is a multi-asset operational ledger design, not a claim of a complete statutory accounting system or approved financial-statement classification. Reporting valuation, tax and formal financial-accounting export require finance review. Application APIs cannot create arbitrary journals.

Supported baseline assets:
- IRR: one whole rial, scale 0.
- GOLD_MG: one milligram of one fixed contractual gold grade, scale 0. The grade is a configured contract identifier, not an assumed purity.
- Future fund units use separate FundId-specific assets with an explicitly configured scale; grams/milligrams do not determine fund precision.

## 2. Account identity and lifecycle

An account instance has AccountId, immutable TemplateCode, AssetId, OwnerType, OwnerId, PurposeKey, NormalSide, CreatedAt and lifecycle status.

AccountId is a Ledger-generated UUID. Enforce uniqueness on (TemplateCode, AssetId, OwnerType, OwnerId, PurposeKey). Customer available/reserved/restricted templates have one instance per CustomerId and asset. Bank and external clearing templates have instances per bank/custodian/merchant. Suspense templates have separate instances per CaseId.

Account asset, owner and normal side cannot change after provisioning. No account deletion or historical-entry mutation is permitted. Customer closure changes eligibility, not ownership history. Provisioning is an idempotent control command; a Application-supplied account ID cannot select another owner's funds.

Holds and evidence-allocation records identify individual claims within aggregate reserve/payable accounts. A balance in an aggregate reserve account is not permission to capture another hold.

## 3. Debit, credit and displayed balance

- Debit-normal balance = total debits - total credits.
- Credit-normal balance = total credits - total debits.
- SignedNetDebit = total debits - total credits for every account.

Customer wallet/gold amounts are positive credit-normal balances. A debit reduces those claims. BankCash is debit-normal: a debit increases cash.

The per-asset conservation test is SUM(SignedNetDebit) = 0. Do not sum positive normal-side balances and expect zero.

Each journal has at least two nonzero lines and equal total Debit/Credit independently per asset. A multi-asset journal must balance IRR and GOLD_MG separately. Zero lines are omitted.

NonNegative applies to the final aggregate account delta under locks, not a line-by-line intermediate value. This allows a single journal to capture and refund already-returned money without exposing a transient spendable balance. Signed conversion controls are exceptions; customer available, reserved and restricted accounts never go negative.

## 4. Baseline IRR account templates

| Code | Name | Category | Normal side | Owner scope | Balance rule |
| --- | --- | --- | --- | --- | --- |
| IRR-1100 | BankCash | Asset | Debit | BankAccount | NonNegative |
| IRR-1110 | GatewayReceivable | Asset | Debit | GatewayMerchant | NonNegative |
| IRR-1120 | CustodianCashReceivable | Asset | Debit | Custodian | NonNegative |
| IRR-1130 | CustodianAdvance | Asset | Debit | Custodian | NonNegative |
| IRR-1190 | SuspenseDebit | SuspenseAsset | Debit | Case | NonNegative |
| IRR-2100 | CustomerRialAvailable | Liability | Credit | Customer | NonNegative |
| IRR-2110 | CustomerRialReserved | Liability | Credit | Customer | NonNegative |
| IRR-2120 | CustomerRialRestricted | Liability | Credit | Customer | NonNegative |
| IRR-2130 | WithdrawalPayable | Liability | Credit | Customer | NonNegative |
| IRR-2140 | RefundPayable | Liability | Credit | Customer | NonNegative |
| IRR-2150 | CustodianPurchasePayable | Liability | Credit | Custodian | NonNegative |
| IRR-2160 | GatewayRefundPayable | Liability | Credit | GatewayMerchant | NonNegative |
| IRR-2170 | OperationsPayable | Liability | Credit | Counterparty | NonNegative |
| IRR-2190 | SuspenseCredit | SuspenseLiability | Credit | Case | NonNegative |
| IRR-3100 | PlatformCapital | Equity | Credit | Platform | NonNegative |
| IRR-3200 | TradingRialClearing | ConversionControl | Credit | Platform | Signed |
| IRR-4100 | FeeRevenue | Income | Credit | Platform | NonNegative |
| IRR-5100 | ProviderFeeExpense | Expense | Debit | Platform | NonNegative |
| IRR-5200 | CorrectionLossExpense | Expense | Debit | Platform | NonNegative |

### Account meanings

- **IRR-1100 BankCash:** Confirmed cash; the only V1 cash source for immediate liquidity.
- **IRR-1110 GatewayReceivable:** Verified captured money not yet settled to the bank; not usable cash.
- **IRR-1120 CustodianCashReceivable:** Confirmed custody-sale proceeds still owed; not usable cash.
- **IRR-1130 CustodianAdvance:** Confirmed cash paid for gold not yet received; not usable cash.
- **IRR-1190 SuspenseDebit:** Proven unresolved debit/loss; not cash and not an asserted collectible customer debt.
- **IRR-2100 CustomerRialAvailable:** Customer claim available for new permitted operations.
- **IRR-2110 CustomerRialReserved:** Claim reserved by a specific customer hold; not freely spendable.
- **IRR-2120 CustomerRialRestricted:** Real funds held under a customer restriction.
- **IRR-2130 WithdrawalPayable:** Accepted payout claim; corresponding physical cash reservation remains until a definitive result.
- **IRR-2140 RefundPayable:** Reserved obligation to return a captured payment.
- **IRR-2150 CustodianPurchasePayable:** Gold received on an accepted purchase but cash not yet paid.
- **IRR-2160 GatewayRefundPayable:** Provider has refunded the payer but platform cash debit is pending.
- **IRR-2170 OperationsPayable:** Other evidenced near-term cash obligations.
- **IRR-2190 SuspenseCredit:** Unidentified cash receipt; offsets cash and does not create treasury headroom.
- **IRR-3100 PlatformCapital:** Confirmed owner funding; not a customer top-up.
- **IRR-3200 TradingRialClearing:** Internal IRR conversion position; neither cash nor a standalone profit measure.
- **IRR-4100 FeeRevenue:** Posted customer fees; reversal cannot exceed related original fees.
- **IRR-5100 ProviderFeeExpense:** Evidenced gateway/bank/custodian costs paid by the platform.
- **IRR-5200 CorrectionLossExpense:** Approved write-off of a proven suspense debit.

## 5. Baseline GOLD_MG account templates

| Code | Name | Category | Normal side | Owner scope | Balance rule |
| --- | --- | --- | --- | --- | --- |
| GOLD-1100 | CustodianGold | Asset | Debit | Custodian | NonNegative |
| GOLD-1110 | CustodianGoldUnidentified | Asset | Debit | Custodian | NonNegative |
| GOLD-1190 | GoldSuspenseDebit | SuspenseAsset | Debit | Case | NonNegative |
| GOLD-2100 | CustomerGoldAvailable | Liability | Credit | Customer | NonNegative |
| GOLD-2110 | CustomerGoldReserved | Liability | Credit | Customer | NonNegative |
| GOLD-2120 | CustomerGoldRestricted | Liability | Credit | Customer | NonNegative |
| GOLD-2190 | GoldSuspenseCredit | SuspenseLiability | Credit | Case | NonNegative |
| GOLD-3200 | PlatformGoldPosition | ConversionControl | Credit | Platform | Signed |

- **GOLD-1100 CustodianGold:** Confirmed eligible pooled gold of the contracted grade.
- **GOLD-1110 CustodianGoldUnidentified:** Physical receipt whose ownership is unresolved; excluded from backing.
- **GOLD-1190 GoldSuspenseDebit:** Unresolved physical shortage/claim; excluded from backing.
- **GOLD-2100 CustomerGoldAvailable:** Customer gold claim available for a permitted sale or reservation.
- **GOLD-2110 CustomerGoldReserved:** Customer gold claim in a specific hold.
- **GOLD-2120 CustomerGoldRestricted:** Customer gold claim restricted from new use.
- **GOLD-2190 GoldSuspenseCredit:** Unidentified ownership obligation; not free platform stock.
- **GOLD-3200 PlatformGoldPosition:** Internal gold residual/conversion position; can be negative within permitted backing exposure.

Earlier names are aliases, not additional accounts:
- UserRial = CustomerRialAvailable.
- UserGold = CustomerGoldAvailable.
- SuspenseIRR must now specify debit versus credit and a CaseId.
- BankCash, GatewayReceivable, TradingRialClearing and PlatformGoldPosition retain their meanings with explicit normal-side rules.

## 6. Authoritative backing calculation

C = SUM(eligible CustodianGold balances).
U = SUM(CustomerGoldAvailable + CustomerGoldReserved + CustomerGoldRestricted).
G = remaining active platform gold-outflow reservations.
Ceff = C - G.
L = floor(Ceff / 100).
RemainingBuyCapacity = max(0, Ceff + L - U).

The original agreed 1% denominator is total confirmed eligible pooled custody, not free stock. With no outgoing reservations, Ceff = C and the agreed example is unchanged: C = 1,000 g and U = 995 g gives 15 g capacity.

Using Ceff while an outgoing movement is reserved is an author-proposed conservative extension: calculate the allowance against the inventory that will remain after already-authorized outflows. Do not both subtract G and subtract the same completed movement again; completion lowers C and consumes the matching reservation atomically.

A new manual treasury gold-outflow reservation may use only free gold: q <= max(0, C - U - G). It cannot deliberately consume customer backing to raise cash. Subsequent customer buys still respect Ceff.

Exclude pending purchases, unidentified gold, suspense debits and expected recoveries from C. Include restricted/reserved customer gold in U.

PlatformGoldPosition is a bookkeeping control, not the backing check. In a clean opening book without suspense, C = U + PlatformGoldPosition. Suspense and unmatched movements can break that simplified relationship; the actual backing guard always uses eligible confirmed custody and customer claims.

New author proposal for baseline consistency: enforce the same 1% maximum in healthy and outage modes. Only the outage rule was explicitly confirmed before this document; changing the ratio remains a versioned policy decision, not a hidden default.

## 7. Authoritative cash and liquidity calculation

B = SUM(confirmed usable BankCash).
CustomerCashClaims = CustomerRialAvailable + CustomerRialReserved + CustomerRialRestricted + WithdrawalPayable + RefundPayable.
OtherCashLiabilities = CustodianPurchasePayable + GatewayRefundPayable + OperationsPayable + SuspenseCredit.
H = active platform cash commitments not already represented in those liability balances.
R = configured operational cash buffer.

FreeLiquidity = B - CustomerCashClaims - OtherCashLiabilities - H - R.
New sale requires FreeLiquidity >= NetCreditIRR.

GatewayReceivable, CustodianAdvance, CustodianCashReceivable, suspense debits and planned funding are not B. Unknown received cash paired with SuspenseCredit creates no free liquidity.

A move from available wallet to WithdrawalPayable or RefundPayable changes classification, not the total customer claim. Do not deduct it again as H.

Maintain a second physical-outflow reservation view:
UnreservedBankCash = B - SUM(active bank cash-outflow reservations) - R.

This includes payout reservations even when their liabilities already appear in CustomerCashClaims. It protects physical dispatch; it is not subtracted a second time in FreeLiquidity. Each reservation records which payable, if any, already represents it.

A new withdrawal/refund needs the customer's claim and sufficient UnreservedBankCash. Platform-paid fee caps additionally need FreeLiquidity. Custodian purchases consume only FreeLiquidity, not cash already backing customer claims.

FreeLiquidity may be negative after a real external loss or a verified but unsettled top-up. Do not fabricate funding to repair it: block new risk-increasing sales/commitments, preserve existing claims and request operations funding. Actual outflows, exceptions and cash constraints have the typed contracts in FINANCIAL_OPERATIONS.

Cash source eligibility, buffer and thresholds are versioned configuration. Proposed local-demo buffer is zero; this is not a production reserve recommendation.

## 8. Suspense and corrections

Suspense is never available customer money, usable cash or physical backing.

Examples:
- Unidentified real bank receipt: Debit BankCash / Credit SuspenseCredit.
- Unexpected real bank debit pending investigation: Debit SuspenseDebit / Credit BankCash.
- Unidentified physical gold receipt: Debit CustodianGoldUnidentified / Credit GoldSuspenseCredit.
- Confirmed unexplained physical shortage: Debit GoldSuspenseDebit / Credit CustodianGold.

Every suspense balance links to a CaseId, external evidence, actor, owner and aging status. Resolution uses a new journal. Write-off is an explicit approved loss, not a balance edit.

A correction reverses proven erroneous accounting; it does not reverse an actual external payment by wish. Full reversal plus optional replacement is atomic, references the original journal and cannot make a protected account negative. If dependent spending prevents reversal, create/freeze a finance case instead of forcing negative customer balances. See the dedicated operation contracts.

## 9. Fund and Card extension templates

These are design contracts, not V1 implementation promises.

| Template | Asset | Normal side | Meaning |
| --- | --- | --- | --- |
| BrokerSubscriptionAdvance | IRR | Debit | Cash sent to brokerage, allocation pending |
| CustomerFundSubscriptionPending | IRR | Credit | Nonwithdrawable customer claim awaiting unit allocation |
| BrokerRedemptionReceivable | IRR | Debit | Confirmed redemption cash not received |
| CustomerFundRedemptionPending | IRR | Credit | Nonwithdrawable claim pending redemption cash |
| BrokerageFundUnits | FUND:<FundId>:MINOR_UNITS | Debit | Confirmed held units |
| CustomerFundAvailable | Same fund asset | Credit | Customer units available |
| CustomerFundReserved | Same fund asset | Credit | Units committed to an outstanding request |
| CustomerFundRestricted | Same fund asset | Credit | Restricted customer units |
| CardSettlementPayable | IRR | Credit | Captured card obligation not yet paid to bank |

Fund pending claims are not spendable IRR. Their cash commitments/receivables require the extension accounting in FINANCIAL_OPERATIONS; do not automatically count them as immediately withdrawable wallet claims.

CardSettlementPayable joins OtherCashLiabilities once Card is activated. Card reserve/capture/release uses hold-specific references. Unit scales, commercial fees and bank deadlines must be configured before activation. No fund receivable can fund instant Card spending without a separately funded liquidity arrangement.

## 10. Persistence and projection ownership

Proposed Ledger tables:
Assets, AccountTemplates, Accounts, Journals, JournalEntries, AccountBalances, FinancialOperations, BusinessActionClaims, QuoteClaims, ExternalActionClaims, Holds, ControlCommands, FinancialPolicies, FinancialCustomerStates, EvidenceAllocations and AdjustmentCases.

AccountBalances is a transactionally maintained projection:
- Amounts and final balance use checked Int64 units.
- Lifetime debit/credit totals and aggregate arithmetic use sufficiently wide exact numeric values, not unchecked long sums.
- All journal writers go through the restricted posting boundary.
- A proposed PostgreSQL post_journal function writes entries and updates aggregate per-account projections exactly once under locks.
- Ledger application credentials cannot directly edit journal entries or account balances; migrations use a separate role.
- Deferred validation checks complete journal balance by asset at commit; ordinary row CHECK constraints alone cannot prove a cross-row sum.
- Financial operation result, financial controls and journal updates share one Ledger transaction.
- Reject unexpected invariant/database errors as technical rollback, not as a falsely persisted business rejection.

Exact SQL/migrations are not implemented here. The chosen enforcement mechanism must be tested against direct invalid writes, duplicate calls, crashes and concurrent posting.

## 11. Review and evidence

[Posting examples](LEDGER_POSTING_EXAMPLES.md) and [machine-readable fixtures](examples/ledger-postings.json) exercise actual account directions and expected customer/cash effects. They validate the proposed accounting, not application code.

Background references:
- [TigerBeetle financial-accounting explanation](https://docs.tigerbeetle.com/coding/financial-accounting/): account-side conventions.
- [PostgreSQL constraints](https://www.postgresql.org/docs/current/ddl-constraints.html): row and cross-row enforcement limits.
- [PostgreSQL constraint triggers](https://www.postgresql.org/docs/current/sql-createtrigger.html): deferred validation support.

The account catalog and formulas above are this project's proposed design; using these references does not select TigerBeetle as the database.

## Future product flow supplements

See [Fund](FUND_PRODUCT_FLOW.md) and [Card](CARD_PRODUCT_FLOW.md). Card refund extension templates add BankCardRefundReceivable (IRR, Debit) and CustomerCardRefundPending (IRR, Credit); neither is cash available to spend. A funded platform Fund purchase additionally uses PlatformFundPosition in the corresponding unit asset, Credit-normal and signed; this is platform inventory ownership, not a second customer claim. These are future-only templates and do not alter the 27 baseline templates.
