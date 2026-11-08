# Data Model — V02

Use separate Application and Ledger migrations. IDs crossing services are values in published contracts. They carry no cross-database foreign keys.

## Application

| Record | Fields and invariants |
| --- | --- |
| Buy quote | Quote ID, customer ID, requested whole IRR, paired price observation reference and timestamp, buy price IRR/g, whole mg, final principal, fee rate and amount, total debit, quote policy version, UTC issue and expiry. Positive input and quantity. Bind all quoted economics at issue. |
| Buy order | Order ID, customer, quote ID, customer idempotency key and canonical request hash/version, stable Ledger operation ID, frozen request, customer acknowledgement, processing state and applied decision. Unique customer/key and unique quote. Resolve a same-key claim before classifying quote reuse. Restrict reads to the owning customer. |
| Buy recovery work | Order ID, due time, lease expiry/version and attempt state. One work item per order. Lease before dispatch and release or reschedule under version check. The frozen request lives on the order. The worker also resumes orders whose known result has not been applied. |
| Procurement intent | Order ID, original Ledger result reference, positive incremental uncovered mg and creation time. Unique order. Created with terminal Application result application only for a positive increment. V06 can group and execute these intents separately. |

Admission persists the claims, immutable operation and work in one transaction before sending. A duplicate customer key compares typed content and returns the original acknowledgement or conflict. Transport failure and ordinary lookup NotFound keep the order unresolved. A stored Posted or Rejected result transitions the order once to Completed or Rejected. Applying Posted and inserting its required procurement intent are one Application-local commit. Repeated application leaves the original order and intent unchanged.

## Ledger

| Record | Fields and invariants |
| --- | --- |
| Effective eligibility | Reuse V01 customer financial eligibility. Read under the financial guard. Effective restriction blocks a new buy decision. |
| Backing guard | One shared guard for the V02 gold pool and protected evidence of eligible confirmed custody, active outgoing custody commitments and custodian mode. Existing claims are derived from, or synchronously maintained with, authoritative customer gold accounts. All writers affecting capacity use the same guard. Controlled test setup initializes synthetic custody and mode without an externally exposed mutation route. |
| Account and projection | Owner, account kind, immutable asset and normal side, exact minor-unit balance. Existing V01 spendable IRR account funds F04. Gold accounts use mg. Spendable customer accounts remain nonnegative. A signed platform gold position is allowed within the backing rule. Balance equals the sum of journal legs signed by normal side. |
| Journal and lines | Journal ID and template, operation reference, account references, debit/credit direction and positive exact minor units. Enforce exact F02/F04 leg semantics and equality of debit and credit totals per asset. Journals and lines are append-only. |
| Buy outcome and claims | Authenticated Trading producer, operation ID, request hash/version, business action and quote references, decision time, reason or journal reference, and posted incremental uncovered mg. Persist one canonical outcome per operation identity. Keep separate unique canonical business-action and posted quote claims so conflicting operation IDs can receive their own durable rejection referencing the original. Preserve V01 capture-specific decisions and lookup. |

### Decision and accounting

1. Begin a short local transaction. Serialize the authenticated operation identity and return its stored result on identical replay. Mismatched content under that identity conflicts.
2. Protect the shared backing resource, effective eligibility and account rows in a stable order. Check canonical business action and quote claims under protection. Sample the server decision time after acquiring guards. Recheck policy, time, eligibility, exact terms, wallet and shared capacity before the first decision.
3. Let C be confirmed eligible custody, G the remaining authorized outgoing reservations and U all current customer claims, in mg. Let E = max(0, C − G). Headroom is max(0, E − U) in normal mode, or max(0, E + floor(E / 100) − U) in evidenced outage mode. Reject where reliable state is unavailable. In-flight procurement, unidentified receipts and suspense supply zero backing. When an outflow completes, reduce confirmed custody and settle its reservation together.
4. For a posted purchase of q, store max(0, U + q − E) − max(0, U − E) as the procurement increment in the decision. Post the IRR debit to CustomerRialAvailable for total, credit TradingRialClearing for principal and credit FeeRevenue for positive fee. Post the mg debit to PlatformGoldPosition and credit CustomerGoldAvailable for q. Omit a zero fee line.
5. Commit journal, signed projections, affected control state, durable claims and terminal decision together. A business rejection records a stable result without financial writes. A fault before commit leaves no partial effect. Keep network calls outside the transaction.

### Upgrade and privileges

Backfill existing accounts and journals as IRR/F02. Use neutral minor-unit field names for the shared storage and migrate dependent internal SQL and fixture reads in the same change. Preserve old row identities, amounts and V01 wire names. Use deferred checks for valid journal shape, per-asset balance and projection reconstruction. Restrict journal and projection writes to a Ledger-owned posting boundary, with explicit execute grants and a fixed, qualified database scope. Expose only the locked state and financial operations the runtime needs. Owner-run migrations retain schema authority. Test permission denial with runtime credentials and invariant enforcement with controlled privileged fixtures.
