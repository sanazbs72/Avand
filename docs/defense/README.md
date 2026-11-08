# Avand V1 - Defense Guide

Date: 2026-11-06

Each decision has its own defense file, separate from the design and decision log.

- [Design](../DESIGN.md)
- [Decision log](../DECISIONS.md)

## How to prepare

For each decision, be able to state the chosen behavior, why it fits V1, the alternative you rejected, the risk it creates, and the evidence you will demonstrate.

Do not claim proposed details are already approved or implemented. The test descriptions below are intended verification, not results. Use your actual work experience; none has been invented here.

## Read by theme

- Customer contract: D001-D005.
- Ledger, custody and capacity: D006-D010.
- Liquidity and operational follow-up: D011-D013.
- Concurrency and external failure: D014-D017.
- Stack and architecture: D018-D019.
- Assignment adaptation and evidence: D020-D022.
- Customer identity and financial eligibility: D023-D024.
- Historical repository layout: D025 (superseded).
- Independent Ledger, recovery, financial authority and delivery scope: D026-D029.
- Detailed accounts, operations, numeric defaults and posting enforcement: D030-D033.

## Individual defenses

| ID | Topic | Status |
| --- | --- | --- |
| D001 | [Fund purchases only from the existing rial wallet](D001-wallet-only-purchases.md) | Confirmed |
| D002 | [Treat the entered purchase amount as gold principal, with an additional fee](D002-principal-plus-fee.md) | Confirmed |
| D003 | [Round traded gold down to whole milligrams and charge for the actual quantity](D003-milligram-rounding.md) | Confirmed |
| D004 | [Round each calculated rial fee up to a whole rial](D004-fee-rounding.md) | Confirmed |
| D005 | [Separate a 30-second quote lock from a 30-second feed freshness limit](D005-quote-lifetime-and-feed-freshness.md) | Confirmed |
| D006 | [Use an append-only double-entry ledger as the financial source of truth](D006-double-entry-ledger.md) | Confirmed principle; detailed D030 account catalog proposed for review |
| D007 | [Maintain balances synchronously in the ledger transaction](D007-transactional-balance-projection.md) | Working proposal supporting the agreed ledger model |
| D008 | [Complete customer trades locally and procure pooled gold asynchronously](D008-pooled-custody-and-immediate-trades.md) | Confirmed policy; procurement aggregation remains proposed |
| D009 | [Bound uncovered gold by 1% of total confirmed custody](D009-one-percent-backing-limit.md) | Confirmed outage policy; D032 healthy-mode/reservation defaults proposed |
| D010 | [Display an advisory maximum and enforce capacity again at confirmation](D010-display-maximum-buy.md) | Confirmed |
| D011 | [Accept a gold sale only when its net proceeds can be immediately withdrawable](D011-liquidity-gated-sales.md) | Confirmed |
| D012 | [Replenish liquidity through operations, with no automatic custody sale](D012-manual-treasury.md) | Confirmed |
| D013 | [Earlier unresolved top-up review policy](D013-payment-review-and-late-success.md) | Historical threshold superseded by D036. Verified capture still posts once |
| D014 | [Deduplicate both API requests and underlying financial operations](D014-idempotency-and-business-deduplication.md) | Retained requirement; response contract refined by D027 |
| D015 | [Keep financial effects atomic inside Ledger and recover Application status separately](D015-atomic-local-posting-and-locks.md) | Financial invariant retained; earlier cross-module transaction superseded by D026-D027 |
| D016 | [Persist dispatch, recovery and external-work intent in the owning service](D016-durable-outbox-and-workers.md) | Working mechanism updated for D026-D027 |
| D017 | [Keep customer trades, top-ups, custody operations and treasury requests separate](D017-separate-operation-lifecycles.md) | Confirmed separation; exact state names proposed |
| D018 | [Use C# on .NET 10 and PostgreSQL](D018-dotnet-postgresql-stack.md) | Confirmed stack; EF Core 10 and Npgsql 10 proposed |
| D019 | [Use a modular monolith and one transactional database for V1](D019-modular-monolith.md) | Superseded historical proposal; replaced by D026 |
| D020 | [Explicitly adapt callback acceptance scenarios to independent wallet top-up](D020-adapted-callback-acceptance-tests.md) | Confirmed deliberate deviation from literal brief wording |
| D021 | [Retain bidirectional reconciliation and traceable operation history](D021-reconciliation-and-history.md) | Retained requirement; detailed workflow remains proposed |
| D022 | [Use exact asset units and a small journal schema](D022-exact-units-and-posting-model.md) | Working proposal |
| D023 | [Separate Customer, Identity and ledger ownership](D023-customer-identity-and-ledger-ownership.md) | Confirmed separation; entity fields and identity mechanism proposed |
| D024 | [Require Active and Verified status for new customer financial instructions](D024-active-verified-financial-operations.md) | Confirmed policy; coordination mechanism proposed |
| D025 | [Use three backend projects with business modules inside Core](D025-project-layout-and-dependencies.md) | Superseded historical folder proposal; replaced by D026 and revised PROJECT_STRUCTURE |
| D026 | [Deploy Ledger independently as the shared financial capability](D026-independent-ledger-service.md) | Confirmed direction; protocol and project details are working design |
| D027 | [Use one atomic financial operation with durable inquiry and forward recovery](D027-atomic-operation-and-forward-recovery.md) | Confirmed approach; exact API and state names are working design |
| D028 | [Enforce spending, backing, liquidity and effective financial restrictions inside Ledger](D028-ledger-financial-control-authority.md) | Working authority design preserving confirmed policies |
| D029 | [Bound the service split to the available team and assignment time](D029-bounded-two-service-delivery.md) | Delivery proposal under the confirmed service direction |
| D030 | [Define typed account templates and separate claims from real backing](D030-explicit-chart-of-accounts.md) | Detailed account catalog proposed for review |
| D031 | [Specify typed financial and control operations with replay-safe outcomes](D031-typed-financial-operation-contracts.md) | Detailed baseline contract proposed under the confirmed Ledger service direction |
| D032 | [Make numeric and capacity defaults explicit for review](D032-exact-rounding-and-financial-guard-defaults.md) | Author-proposed defaults; prior confirmed policies retained |
| D033 | [Restrict journal writes and verify projections inside the Ledger transaction](D033-controlled-journal-posting-and-projections.md) | Database enforcement and lock strategy proposed |

| D034 | [Model Fund allocation and delayed cash as explicit pending claims](D034-fund-lifecycle.md) | Future product design proposal; V1 implementation excluded |
| D035 | [Authorize Card spending through atomic funded holds and separate bank finality](D035-card-authorization-and-settlement.md) | Future product design proposal; V1 implementation excluded |
| D036 | [Apply company-confirmed V1 product rules](D036-company-confirmed-v1-rules.md) | Company-confirmed Q1–Q17. Design interpretations are labeled separately |

## Questions that span several decisions

**Does a balanced ledger prove there is enough physical gold or cash?**
No. Ledger balance, physical backing, liquidity and external reconciliation are separate checks.

**Is the solution literally compliant with every original callback scenario?**
No. Wallet-only top-up changes those scenarios. D020 records the deliberate deviation and its evaluation risk.

**What is complete today?**
A documented checkpoint of the discussion. The financial journal examples and behavioral contracts are design material, not a claim of passing tests or a finished take-home submission.

**Why manual treasury?**
The V1 scope deliberately keeps liquidation decisions with operations while making shortages observable and refusing unsupported new sale obligations.

**What important work remains?**
Read DESIGN section 16. The baseline chart and financial operation contract now have full draft definitions in CHART_OF_ACCOUNTS and FINANCIAL_OPERATIONS. New policy defaults still need review and implementation. Fund/Card activation details, security, reconciliation operations and the 12-week plan remain broader open work.

**Can a suspended customer still receive a verified payment result?**
The system must account for genuine activity initiated earlier. Eligibility blocks new instructions; it does not erase money that arrived. The restriction policy for those funds still needs a separate contract.

**Does independent deployment force a Saga engine?**
No. The selected V1 protocol keeps financial legs atomic in Ledger and repairs Application state through durable inquiry/retry. Richer Fund/Card commitments may need process-manager or Saga-style coordination later. D027 explains the limit.

**Why change the initial monolith recommendation with such a small team?**
D026 explains the common financial boundary and the cost accepted. D029 limits the change to two hosts, one repository/stack and no mandatory broker/Saga infrastructure. The first proof must include recovery and concurrency, not just a happy-path demo.

**Did the original numerical examples satisfy the minimum?**
The earlier 490,000 IRR sell example was a teaching journal below the source 1,000,000 IRR minimum. The current executable-design examples use a 4,900,000 IRR sale and independently check rounding/minimum boundaries.

**Does a failed payment always create a refund journal?**
No. If Avand never captured money, a provider return is evidence in Application, not a platform bank debit. If capture and return are both verified, record their real effects once; if money was already spent, use the evidenced suspense/restriction workflow rather than a negative wallet.

## Suggested presentation sequence

1. Explain wallet-only funding and immediate customer trade finality.
2. Show one buy and sell journal, balanced separately per asset.
3. Explain synchronous balances and protected shared budgets.
4. Walk through duplicate callback, concurrent sell, stale quote and Unknown external outcome.
5. Show the 1% backing example and manual treasury boundary.
6. Explain the two-service boundary, response-loss recovery and bounded delivery scope.
7. Identify the deliberate callback adaptation and remaining risks candidly.
8. Explain how Customer eligibility differs from login and why later callbacks remain accountable after suspension.

This is preparation material. Do not read it as a script or substitute generic claims for your own experience.

Future-only product flows and defenses: D034 Fund and D035 Card. No implementation added to V1.
