# Research — V03 sell gold for immediately usable IRR

## R1. Exact sell arithmetic and input modes

**Decision** — Add `src/Shared/SellQuoteMath.cs`, compiled into both hosts like `BuyQuoteMath`. With bid A (IRR/g), rate R ppm and weight q mg: gross G = floor(q × A / 1000), fee F = ceil(G × R / 1,000,000), net N = G − F. For a rial target T, q = floor(1000 × T / A). For weight input, q is the entered whole mg. Reject q < 1, a non-positive or fractional T, N ≤ 0 or G outside the inclusive limits. Use `Int128` intermediates and checked conversion. The initial policy version is `sell-v1` at 5,000 ppm with the shared 1,000,000–5,000,000,000 IRR limits. Ledger recomputes G, F and N from q, A and R and never trusts the requested amount. Application binds the input mode and value at issuance.

**Rationale** — C-01 and C-02 fix the limit basis (gross) and the rounding directions. The [F05 contract](../../docs/FINANCIAL_OPERATIONS.md#f05-sellgold) and section 2 of the financial contract already use these formulas. One source used in both hosts prevents the quote and the posting from drifting apart.

**Alternatives considered** — Generalize `BuyQuoteMath` into one side-aware class. That touches tested V02 code for no behavioral gain. Separate files that share only constants keep both sides readable.

## R2. Free liquidity definition and storage

**Decision** — Per C-03 and [section 7 of the chart of accounts](../../docs/CHART_OF_ACCOUNTS.md#7-authoritative-cash-and-liquidity-calculation): FreeLiquidity = B − CustomerCashClaims − OtherCashLiabilities − H − R. B is the `BankCash` balance. CustomerCashClaims is the sum of customer `CustomerRialAvailable`, `CustomerRialReserved` and `CustomerRialRestricted` plus any `WithdrawalPayable` and `RefundPayable`. OtherCashLiabilities is the sum of any `CustodianPurchasePayable`, `GatewayRefundPayable`, `OperationsPayable` and `SuspenseCredit`. Buckets that don't exist yet contribute zero; the formula already names them so later slices need no change. H (active unbooked cash commitments) and R (operational buffer, demo 0) live on a single `LiquidityControl` row. Compute the value in one SQL statement inside a `SECURITY DEFINER` function, called after taking the guard.

**Rationale** — D011 requires confirmed cash minus distinct obligations. `GatewayReceivable`, which is what V01 top-ups debit today, is not B. So until gateway settlement (F03) exists, free liquidity comes only from controlled cash funding. That matches the PRD's demonstration prerequisite.

**Alternatives considered** — A maintained running counter. It's faster, but it adds a second source of truth that every posting must update. Summing projections under the guard is exact and cheap at V1 volume.

## R3. Concurrency: who takes the liquidity guard

**Decision** — Add `avand_lock_liquidity()` (`FOR UPDATE` on the single `LiquidityControl` row). Every writer that can lower free liquidity must take it: F05 sell (raises customer claims), V01 F02 capture (credits customer IRR against a receivable) and any later change to B, H or R. Writers that can only raise free liquidity need not take it. F04 buy moves customer IRR into clearing and fee. Global lock order: operation or producer lock → backing guard → liquidity guard → eligibility → accounts by ID. A sell also takes the backing guard, because V02 requires every writer of customer gold claims to hold it.

**Rationale** — Serializing all decreasers means two sells, or a sell and a top-up, can't both pass against the same headroom. A buy that commits during a sell's read can only make the sell's view conservative. One fixed order across F02, F04 and F05 prevents deadlocks.

**Alternatives considered** — Advisory lock per producer: it doesn't cover the cross-producer writers (Payments vs Trading). Serializable isolation with retries: it's correct but adds retry paths to the V01 and V02 code without benefit.

## R4. F05 posting and the existing boundary

**Decision** — Extend `avand_post_journal` and the deferred template check to accept `F05`. Debit `CustomerGoldAvailable` q and credit `PlatformGoldPosition` q. Debit `TradingRialClearing` G, credit `CustomerRialAvailable` N and credit `FeeRevenue` F, omitting a zero fee. Require one sell decision referencing the journal. Add `BankCash` (debit-normal IRR, non-negative) and `PlatformCapital` (credit-normal IRR) and update `CK_Accounts_Asset`. Add a fixture-only `OpeningCash` template (debit `BankCash`, credit `PlatformCapital`) that the runtime role can't post.

**Rationale** — This keeps one controlled journal and one posting boundary. The runtime keeps no direct journal or projection DML, as V02 established.

**Alternatives considered** — Expose F01 `ConfirmCapitalInflow` now. It requires Finance-scoped evidence and a producer this slice doesn't have, so it belongs with V07.

## R5. Application records and recovery

**Decision** — Add `SellQuote`, `SellOrder` and `SellRecoveryWork` tables mirroring the V02 buy records, with unique customer/key, unique quote and one work item per order. Reuse the delivery and worker structure from `BuyOrderDelivery` and `BuyRecoveryWorker`, with no procurement intent. Send `SellGold` through the existing `LedgerTradingClient` and `Application.Trading` credential, with business reference `GoldSellOrder` / order ID / `Execute`.

**Rationale** — A separate business-reference kind keeps buy and sell claim namespaces apart. Separate tables leave V02 migrations and regression untouched.

**Alternatives considered** — A unified `GoldOrder` table with a side column. It's cleaner long term but needs a data migration of live V02 rows. Revisit after V04.

## R6. Immediate availability and verification

**Decision** — Prove AC-03 by posting a sell and then completing a V02 buy whose total debit is covered only by the sell's net. Also check that the net sits in `CustomerRialAvailable`, the bucket V04 will reserve from. Withdrawal reservation itself is verified in V04, as the PRD assigns. Use real PostgreSQL with independent connections and barriers for the races, and prove commit before dropping replies. Static arithmetic results are recorded separately from runtime results.

**Rationale** — The repository has no withdrawal flow yet. A balance-bucket check plus a real spend gives observable evidence without pulling V04 into scope.
