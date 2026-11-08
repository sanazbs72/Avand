# Implementation Plan — V02 wallet-funded gold purchase

**Feature** — `002-buy-gold-wallet` | **Git branch** — `main` | **Date** — 2026-11-08 | **Spec** — [approved V02 behavior](spec.md)

## Summary

Application issues and stores a timed buy quote from the buy side of a paired price observation. Confirmation admits one immutable order and uses the V01 wallet through a scoped Ledger BuyGold operation. Ledger protects the first decision against expiry, insufficient funds and shared backing races. It posts both assets and stores the durable result together. Application recovers that result and records the incremental procurement need exactly once.

## Technical Context

| Area | Choice |
| --- | --- |
| Runtime and dependencies | Existing .NET 10 ASP.NET Core hosts, EF Core/Npgsql and built-in hosted workers. Use a pure source-linked arithmetic file in both hosts. |
| Storage and deployment | Two independently deployed services with their own PostgreSQL databases, runtime credentials and owner-run migrations. Use existing V01 stores and wallet. |
| Provider input | Application paired-price adapter with controlled synthetic observations. Ledger admits against controlled, confirmed custody and evidenced mode. No live custodian integration in this slice. |
| Test platform | Existing xUnit/Testcontainers integration project, real PostgreSQL 17.6 and both HTTP hosts. Add V02 fixtures for clocks, price observations, controlled custody and targeted failures. |
| Required timing and load | Separate 30-second price freshness and quote lifetime. Exercise 100 identical concurrent confirmations and distinct-operation races. Collect results without assigning an unapproved latency or throughput target. |
| Delivery scope | Customer and internal HTTP contracts, exact accounting, durable recovery and follow-up intent. Existing API hosts provide the customer journey contract. Frontend rendering and external procurement execution are outside this repository slice. |

## Constitution Check

| Gate | Phase 0 entry | Phase 1 design check |
| --- | --- | --- |
| I–II, traceability and scope | Approved V02 spec and V01 dependency identified. | [Research](research.md) reconciles draft arithmetic and backing defaults. [Validation](quickstart.md) traces AC-01–16. |
| III–IV, ownership and contracts | Keep separate stores and service privileges. | [Contract](contracts/v02.md) scopes Trading access. Existing payment routes remain compatible and no host imports the other host's code or data. |
| V–VI, financial integrity | Exact units, protected funds and backing, per-asset balance and atomic outcome required. | [Data model](data-model.md) keeps one controlled Ledger journal and projection, shares the V01 wallet and puts first decision, effects and replay state in one Ledger transaction. |
| VII–VIII and X, recovery | Persist original intent and identity before dispatch. | Local admission and result application are atomic. Same-identity recovery uses the immutable decision. Procurement creation shares Application result application. |
| IX, financial capacity and eligibility | Confirmed custody and effective eligibility govern admission. | Backing and account updates use a fixed lock order. Pending procurement and unidentified gold cannot supply capacity. |
| XI–XIII, verification and security | Synthetic inputs, scoped access and real boundary checks required. | Named acceptance checks include migration, races, lost responses, storage privileges and V01 regression. Runtime evidence remains to be collected during implementation. |

All gates are satisfied by the Phase 1 design. No exception is required.

## Project Structure

### Documentation (this feature)

```text
specs/002-buy-gold-wallet/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── contracts/v02.md
└── quickstart.md
```

### Source Code (repository root)

```text
src/
├── Avand.Application.Api/
│   ├── ApplicationHost.cs
│   ├── Payments/
│   ├── Trading/                   # planned quotes, admission, status, delivery and recovery
│   └── Persistence/Migrations/
├── Avand.Ledger.Api/
│   ├── LedgerHost.cs
│   ├── Controls/
│   ├── Payments/
│   ├── Trading/                   # planned BuyGold and backing decision
│   └── Persistence/Migrations/
└── Shared/                        # planned pure quote arithmetic, compiled into each host
tests/
└── Avand.V01.IntegrationTests/      # existing project, add V02-named classes and fixtures
```

**Structure decision** — Keep the existing deployment and solution projects. Add a small Trading capability to each owner. Share only exact calculation source and fixture inputs. Extend existing route mapping and controlled journal behavior where V01 and V02 share an actual boundary.

## Implementation Sequence

1. Establish exact arithmetic, approved policy defaults, deterministic inputs and V01 compatibility baselines.
2. Add the owner-run Ledger migration and runtime grants. Protect multi-asset journal posting, backing state and wallet serialization. Verify upgrade with populated V01 data.
3. Implement the typed Trading route and durable BuyGold decision. Verify guard order, arithmetic, post/reject replay and per-asset effects.
4. Implement customer quote, admission, owned status and balance reads through Application. Exercise top-up through V01 followed by purchase through V02.
5. Implement same-operation recovery and atomic procurement intent. Run the full [acceptance matrix](quickstart.md), record observed evidence and update generated tasks after verification.
