---
name: avand-architecture-boundary-review
description: Use when an Avand change touches Application or Ledger ownership, service contracts, database access, project references, module boundaries, or the implementation scope of Gold, Fund or Card.
---

# Avand Architecture Boundary Review

Trace a changed behavior across the actual entry point, owner, contract, persistence and deployment. Review the affected path and classify findings against the approved design.

## Establish what governs the change

Read the active feature specification and plan, then sections II–IV and VII of the [Constitution](../../../.specify/memory/constitution.md). Use the [Decision Log](../../../docs/DECISIONS.md) for decision status, [Ledger Service](../../../docs/LEDGER_SERVICE.md) for the service boundary, and [Project Structure](../../../docs/PROJECT_STRUCTURE.md) for the proposed layout. Verify the actual project graph and deployment artifacts when they exist. A proposed assembly tree or a stale status note cannot override an approved boundary or establish the current repository state.

## Trace ownership and dependencies

| Boundary | Review check |
| --- | --- |
| Application and Ledger | Application owns customer profile, product intent, orders, provider integrations and workflow. Ledger owns accounts, journals, spendable balances, holds, financial controls and durable financial results. Trace each read and write to its owner. |
| Service persistence | Inspect connection configuration, database credentials, migrations and code paths for direct access in either direction. Read-only SQL, cross-database views, shared DbContext and shared transactions cross the boundary. Two databases may share a PostgreSQL instance while keeping access and migrations separate. |
| Service contracts | Exchange versioned DTOs through a supported API or approved integration contract. Check ownership and compatibility. Shared wire types are acceptable when deliberately owned and kept free of domain entities, EF models, repositories and Infrastructure references. |
| Local architecture | Domain and use cases depend on narrow ports. Transport maps requests and applies authentication. Infrastructure implements the ports. A host composition root may reference its own Infrastructure for wiring. Check actual use rather than flagging every Infrastructure reference. |
| Application modules | Product modules use explicit contracts for cross-module work and each owns its private state. Sharing one deployment does not permit direct writes to another module's data or a common product order model that erases distinct lifecycles. Judge boundaries separately from the number of projects. |
| V1 scope | Gold and required supporting flows are in implementation scope. Fund and Card are design-only. Their contracts and workflows can be designed. Empty runtime projects, endpoints, workers and migrations for those products need an explicit scope change. |

Financial operations remain Ledger-local and atomic. Application persists intent before dispatch and applies the durable result in its own transaction. A status update, cached read or UI projection cannot decide that funds moved or authorize spending. Use the Ledger contract for authoritative balances and decisions. When reviewing failure handling, follow the existing operation recovery contract.

## Report and verify

For each finding, name the affected entry point and exact dependency, query or deployment setting. State the governing decision, affected owner, practical failure, smallest correction and needed evidence. Separate a violation of a confirmed decision from a proposed layout choice or a missing requirement. Keep review read-only unless fixes are requested.

For example, a balance display that queries Ledger tables under a read-only grant couples Application to Ledger schema and bypasses its contract. Route the read through Ledger API and remove the grant and persistence dependency. Check project and package references, trace the endpoint call and validate deployed permissions against Ledger data using the Application identity. A passing build verifies compilation. It cannot establish runtime database isolation. Report the actual checks and mark unexecuted ones as unrun.
