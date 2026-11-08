# Implementation Plan — V03 sell gold for immediately usable IRR

**Feature** — `003-sell-gold-wallet` | **Git branch** — `main` | **Date** — 2026-11-08 | **Spec** — [clarified V03 behavior](spec.md)

## Summary

Application issues a timed sell quote from the bid side of the paired price, using either a whole-mg weight or a gross IRR target. Confirmation admits one immutable sell order and sends a scoped Ledger `SellGold` (F05) operation. In one protected transaction, Ledger checks the quote, limits, the customer's available gold and platform free liquidity against the net credit. It then posts the gold and IRR legs and stores the durable result. The net IRR lands in the existing V01 `CustomerRialAvailable` wallet, so V02 buys and future V04 withdrawals can use it immediately. Application reuses the V02 admission, delivery and leased-recovery pattern with sell-owned records.

The main new Ledger capability is free liquidity. The repository has no `BankCash` or `PlatformCapital` account and no liquidity guard yet. This slice adds both, plus controlled fixture-only cash funding. Every posting that can reduce free liquidity takes the new guard.

## Technical Context

| Area | Choice |
| --- | --- |
| Runtime and dependencies | Existing .NET 10 hosts, EF Core/Npgsql and hosted workers. Add a pure source-linked `SellQuoteMath` beside `BuyQuoteMath` in `src/Shared`. No new packages. |
| Storage and deployment | Same two services and databases. One owner-run Ledger migration and one owner-run Application migration. Reuse the V01 wallet, V02 gold accounts, backing guard and posting boundary. |
| Provider input | Existing `PairedPriceSource` bid side with V02's 0–30 second provider-age rule. Bank cash comes only from controlled, balanced fixture setup. There is no live bank or Finance route in this slice. |
| Test platform | Existing `Avand.V01.IntegrationTests` project, PostgreSQL 17.6 and both HTTP hosts. Add V03-named classes and extend `V02Fixture` with controlled bank-cash funding and a real V02 buy to give customers gold. |
| Required timing and load | 30-second quote lifetime, 100 identical concurrent confirmations, and races on one customer's gold and on shared liquidity. No unapproved latency target. |
| Delivery scope | Customer and internal HTTP contracts, exact F05 accounting, liquidity guard and durable recovery. Withdrawal execution (V04), treasury replenishment (V07) and custodian sales are out of scope. |

## Constitution Check

| Gate | Phase 0 entry | Phase 1 design check |
| --- | --- | --- |
| I–II, traceability and scope | Clarified V03 spec with C-01–C-03. V01/V02 dependencies are implemented. | [Research](research.md) maps the answers onto F05 and section 7 of the chart of accounts. [Quickstart](quickstart.md) traces AC-01–13. Withdrawal execution stays in V04. |
| III–IV, ownership and contracts | Application owns sell orders, Ledger owns liquidity and posting. | [Contract](contracts/v03.md) adds sell routes and `SellGold` under the existing Trading credential. Neither host reads the other's store. |
| V–VI, financial integrity | Exact integer units, per-asset balance, atomic outcome. | [Data model](data-model.md) adds the F05 template (5 legs, two assets) to the existing posting boundary and deferred checks. Decision, claims, journal and projections commit together. |
| VII–VIII and X, recovery | Persist intent and identity before dispatch. | Sell admission and result application are Application-local commits. Same-ID replay returns the stored decision before current guards. There is no external effect. |
| IX, cash, backing and eligibility | Liquidity must be confirmed cash minus obligations (D011). | A single `LiquidityControl` guard covers every writer that raises customer cash claims or lowers bank cash, including V01 capture. Receivables, planned funding and suspense debits contribute nothing. |
| XI–XIII, verification and security | Synthetic data, real PostgreSQL races and two-service recovery. | Named checks cover migration over populated V01/V02 data, runtime DML denial, races, lost responses and V01/V02 regression. Runtime evidence is NotRun until implementation. |

All gates are satisfied. No exception is required.

## Project Structure

### Documentation (this feature)

```text
specs/003-sell-gold-wallet/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── contracts/v03.md
├── quickstart.md
└── checklists/requirements.md
```

### Source Code (repository root)

```text
src/
├── Shared/
│   └── SellQuoteMath.cs              # new: bid-side weight/gross/fee/net, both input modes
├── Avand.Application.Api/
│   ├── Trading/
│   │   ├── SellQuoteService.cs       # new
│   │   ├── BuyQuoteService.cs        # extend Application TradingPolicy with sell-v1 rate
│   │   ├── SellOrderAdmission.cs     # new
│   │   ├── SellOrderDelivery.cs      # new
│   │   ├── SellRecoveryWorker.cs     # new, V02 lease pattern
│   │   ├── SellEndpoints.cs          # new
│   │   └── LedgerTradingClient.cs    # extend with SellGold send/inquiry
│   └── Persistence/                  # sell quote, order, work + migration
├── Avand.Ledger.Api/
│   ├── Trading/
│   │   ├── SellGold.cs               # new F05 decision and posting
│   │   ├── LiquidityState.cs         # new free-liquidity read under guard
│   │   └── TradingFinancialState.cs  # extend Ledger TradingPolicy with sell-v1 rate
│   ├── Payments/
│   │   ├── LedgerPaymentEndpoints.cs # dispatch SellGold
│   │   └── GatewayCapturePosting.cs  # take liquidity guard before eligibility
│   └── Persistence/                  # accounts, LiquidityControl, SellDecisions + migration
tests/Avand.V01.IntegrationTests/     # V03*Tests.cs, fixture cash funding
```

**Structure decision** — Add sell-owned records next to the V02 buy records instead of refactoring the buy tables, so V02 behavior and its regression suite stay stable. Share only the exact arithmetic source and the existing posting boundary, internal dispatcher, eligibility and account locks.

## Implementation Sequence

1. Add `SellQuoteMath` with an independent test oracle (AC-01, AC-01b, AC-04 arithmetic). Capture V01/V02 regression baselines.
2. Ledger migration: `BankCash` and `PlatformCapital` accounts, `LiquidityControl` guard (buffer R, unbooked commitments H), F05 template and deferred checks, `SellDecisions` with business and quote claims, a fixture-only `OpeningCash` template, and scoped grants. Make V01 capture take the liquidity guard. Verify migration over populated data and runtime DML denial.
3. Implement `SellGold` with the fixed lock order and free-liquidity rule, and dispatch it through the internal route. Verify guard order, exact boundaries, and post and reject replay.
4. Implement sell quote, admission, delivery, order status and endpoints in Application. Run the end-to-end journey: V01 top-up → V02 buy → V03 sell → V02 buy funded only by the proceeds.
5. Implement sell recovery. Run races, the 100-confirmation burst and lost-response cases from the [acceptance matrix](quickstart.md), then the V01/V02 regression. Record evidence in `harness/evidence/003-sell-gold-wallet.md`.
