# Data Model — V03

Separate Application and Ledger migrations. Cross-service IDs are contract values with no cross-database foreign keys.

## Application

| Record | Fields and invariants |
| --- | --- |
| Sell quote | Quote ID, customer ID, input mode (`Weight` or `GrossIrr`) and entered value, price observation reference and time, bid IRR/g, whole mg, gross, fee rate ppm and amount, net, policy version, UTC issue and expiry (issue + 30 s). All economics are bound at issue. |
| Sell order | Order ID, customer, quote ID, idempotency key and canonical request hash, stable Ledger operation ID, frozen request, acknowledgement, state (`Processing`, `FinancialOutcomeUnknown`, `Completed`, `Rejected`) and applied decision with reason. Unique customer/key and unique quote. Owner-only reads. |
| Sell recovery work | Order ID, due time, lease expiry/version and attempts. One per order. Same lease/version protocol as the V02 buy work. |

Admission persists the claims, frozen operation and work in one transaction before sending. A terminal Ledger result is applied once. Transport failure or inquiry NotFound keeps the order unresolved.

## Ledger

| Record | Fields and invariants |
| --- | --- |
| Accounts (additions) | `BankCash`: platform, IRR, debit-normal, non-negative. `PlatformCapital`: platform, IRR, credit-normal, non-negative. Customer `CustomerGoldAvailable` (V02) and `CustomerRialAvailable` (V01) are reused. `CK_Accounts_Asset` is updated so the debit-normal set is `GatewayReceivable`, `CustodianGold` and `BankCash`. |
| Liquidity control | Single row (Id = 1): `CommitmentsIrr` H ≥ 0, `BufferIrr` R ≥ 0 (demo 0) and a version. Locked by `avand_lock_liquidity()`. Changed only by owner or controlled fixture authority in this slice. |
| Sell outcome and claims | Producer, operation ID, fingerprint, order ID, quote ID, customer ID, decision time, journal reference or rejection code, original operation for conflicts. Unique business claim per order and unique quote claim, as in V02. |
| Journal template F05 | Exactly: debit `CustomerGoldAvailable` q, credit `PlatformGoldPosition` q, debit `TradingRialClearing` G, credit `CustomerRialAvailable` N, and credit `FeeRevenue` F when F > 0. Each asset balances. The journal is referenced by exactly one sell decision. |
| Template OpeningCash | Debit `BankCash`, credit `PlatformCapital`. Accepted only from fixture or owner authority. The runtime role has no direct journal insert. |

### Decision and accounting

1. Begin a short transaction. Lock the operation identity and return the stored result on identical replay. A different fingerprint under the same identity is a conflict.
2. Lock in order: backing guard → liquidity guard → eligibility → the customer's `CustomerGoldAvailable` and `CustomerRialAvailable` rows by ID. Read the business and quote claims, then compute free liquidity in one statement.
3. Sample decision time after the locks. In this order, reject with: `DuplicateBusinessAction` or `QuoteAlreadyUsed`, `UnsupportedQuotePolicy`, `QuoteTermsInvalid` (recomputed G/F/N mismatch, lifetime ≠ 30 s, issue in the future), `QuoteExpired` (now ≥ expiry), `CustomerNotEligible`, `OrderOutOfBounds` (G outside limits or N ≤ 0), `InsufficientGold` (available < q), `InsufficientLiquidity` (FreeLiquidity < N).
4. On acceptance, open the customer IRR account if it's missing and post F05. Customer gold claims drop by q under the backing guard. No procurement intent is created.
5. Commit the journal, projections, claims and decision together. A rejection stores a stable decision with no financial writes. A fault before commit leaves nothing.

FreeLiquidity = `BankCash` − Σ customer IRR claim buckets − Σ other cash liability buckets − H − R. Buckets that don't exist count as zero. `GatewayReceivable` and other receivables never add.

### Writers that take the liquidity guard

| Writer | Effect on free liquidity | Change in this slice |
| --- | --- | --- |
| F05 SellGold | − N | New, takes the guard |
| F02 capture credit | − credited amount (receivable is not cash) | Add the guard after the producer lock, before eligibility. Capture never rejects on liquidity; free liquidity may go negative (spec edge case). |
| F04 BuyGold | + total debit | No guard needed (can only increase) |
| OpeningCash fixture | + amount | Takes the guard |

### Upgrade and privileges

The owner-run migration adds the accounts, the control row, `SellDecisions` and claim tables, extends the template function and deferred checks for F05 and OpeningCash, and adds `avand_lock_liquidity()` and `avand_free_liquidity()` with execute granted to the runtime role and revoked from PUBLIC. Existing journals, decisions and V01/V02 wire shapes are unchanged. Test the upgrade over populated V01 and V02 stores and test runtime denial of direct journal, projection and control-row writes.
