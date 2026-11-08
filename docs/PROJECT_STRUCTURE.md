# Avand V1 - Project Structure

Date: 2026-11-06
Status: Two backend deployments confirmed. Detailed project layout is a working design; source projects have not been generated.

[Design](DESIGN.md) | [Ledger service contract](LEDGER_SERVICE.md) | [Decision log](DECISIONS.md)
[Current service decision D026](defense/D026-independent-ledger-service.md) | [Historical layout D025](defense/D025-project-layout-and-dependencies.md)

## Repository tree

```text
Avand/
|-- Avand.sln
|-- global.json
|-- Directory.Build.props
|-- Directory.Packages.props
|-- docker-compose.yml
|-- README.md
|-- AI.md
|-- CHANGES.md
|-- src/
|   |-- Application/
|   |   |-- Avand.Application.Api/
|   |   |   |-- Endpoints/
|   |   |   |-- Authentication/
|   |   |   '-- Program.cs
|   |   |-- Avand.Application.Core/
|   |   |   |-- Abstractions/
|   |   |   '-- Modules/
|   |   |       |-- Customers/
|   |   |       |-- Pricing/
|   |   |       |-- Trading/
|   |   |       |   |-- Domain/
|   |   |       |   '-- UseCases/
|   |   |       |-- Payments/
|   |   |       |-- Custody/
|   |   |       |-- Treasury/
|   |   |       |-- Reconciliation/
|   |   |       '-- Support/
|   |   '-- Avand.Application.Infrastructure/
|   |       |-- Persistence/
|   |       |   |-- ApplicationDbContext.cs
|   |       |   |-- Configurations/
|   |       |   '-- Migrations/
|   |       |-- Identity/
|   |       |-- LedgerClient/
|   |       |-- Integrations/
|   |       |   |-- PriceProvider/
|   |       |   |-- Gateway/
|   |       |   '-- Custodian/
|   |       |-- Outbox/
|   |       '-- Workers/
|   |           |-- FinancialRecoveryWorker.cs
|   |           |-- PaymentInquiryWorker.cs
|   |           |-- ProcurementWorker.cs
|   |           '-- ReconciliationWorker.cs
|   |-- Ledger/
|   |   |-- Avand.Ledger.Api/
|   |   |   |-- Endpoints/
|   |   |   |-- Authentication/
|   |   |   '-- Program.cs
|   |   |-- Avand.Ledger.Core/
|   |   |   |-- Domain/
|   |   |   |-- FinancialControls/
|   |   |   '-- UseCases/
|   |   |-- Avand.Ledger.Infrastructure/
|   |   |   |-- Persistence/
|   |   |   |   |-- LedgerDbContext.cs
|   |   |   |   |-- Configurations/
|   |   |   |   '-- Migrations/
|   |   |   '-- Posting/
|   |   '-- Avand.Ledger.Contracts/
|   |       '-- V1/
|   |           |-- Operations/
|   |           |-- Balances/
|   |           '-- Controls/
|-- tests/
|   |-- Avand.Application.UnitTests/
|   |-- Avand.Ledger.UnitTests/
|   '-- Avand.IntegrationTests/
|       |-- Scenarios/
|       |-- Concurrency/
|       |-- Recovery/
|       '-- Fixtures/
|-- apps/
|   '-- avand-demo/
'-- docs/
    |-- README.md
    |-- DESIGN.md
    |-- DECISIONS.md
    |-- PROJECT_STRUCTURE.md
    |-- LEDGER_SERVICE.md
    '-- defense/
```

Only documentation exists at this checkpoint. This tree is a proposed source layout. Root build/configuration files and the demo app remain future deliverables.

## Dependencies

| Project | References and responsibility |
| --- | --- |
| Application.Core | Application domain/use cases and ports; no EF, Ledger database or Ledger domain dependency |
| Application.Infrastructure | Application.Core and Ledger.Contracts; implements HTTP adapter, Application persistence, external mocks and workers |
| Application.Api | Application.Core and Application.Infrastructure for composition and HTTP |
| Ledger.Core | Financial rules/use cases and ports; no Application or EF dependency |
| Ledger.Infrastructure | Ledger.Core; owns financial persistence and transaction implementation |
| Ledger.Api | Ledger.Core, Ledger.Infrastructure and Ledger.Contracts; authenticates producers and maps wire DTOs |
| Ledger.Contracts | Versioned transport DTOs only; no EF entities, DbContext, Application or Ledger domain model |

Application.Core uses a narrow local port for financial operations. Application.Infrastructure translates it into HTTP DTOs. Do not share domain entities to simulate an independent service.

Core combines Domain and UseCases within each service to limit project overhead. Modules inside Application are organizational boundaries; independent deployment is reserved for Ledger. Clean Architecture dependency direction applies inside both services.

## Transaction and persistence ownership

ApplicationDbContext owns customer profiles, quotes, orders, gateway/custody evidence, treasury requests, dispatch/recovery records and procurement outbox.

LedgerDbContext owns accounts, journals, balances, financial operation results, effective financial eligibility and authoritative financial controls.

Each owns migrations and database credentials. No shared context, cross-database SQL or distributed transaction exists. Local PostgreSQL may host both databases in one instance; this is not engine-level availability isolation.

The earlier proposal that an order and financial journal commit together is superseded. See the three-commit protocol in LEDGER_SERVICE: durable Application admission, atomic Ledger execution, durable Application result application.

## Runtime and external work

Proposed local compose services:
- application-api
- ledger-api
- postgres, with separately owned product and ledger databases

The demo frontend calls Application API only. Exact frontend tooling is not selected.

Application hosts the recovery and provider workers. FinancialRecoveryWorker queries/retries permanent OperationIds. ProcurementWorker consumes Application-owned work created after financial result application. There is no automatic treasury-liquidation worker.

The baseline uses synchronous HTTP with database-backed recovery, not a broker. A Ledger outbox is required only if Ledger later publishes integration events; V1 recovery does not depend on event delivery.

Mocks live behind Application integration ports. Simulation endpoints must be controlled and environment-scoped. They trigger provider behavior, not direct balance edits.

## Test boundaries

- Application unit tests: intent, quote and result-application rules.
- Ledger unit tests: rounding, journals, balance/control invariants and operation decisions.
- Integration tests: real PostgreSQL databases and both service boundaries; include concurrency, response loss after commit, restarts and recovery.
- Database fixtures seed explicit funding/custody opening entries.

Do not claim testing a Ledger handler in isolation proves cross-service recovery. No executable tests have been created or run.

## Time and ownership

One repository and shared build conventions limit setup cost. There are seven proposed production projects but only two deployable hosts. Fund/Card stay at design level. The lead owns the backend boundary; the frontend engineer and designer are not counted as additional backend engineers.

Do not create an independent deployment for every folder. The selected split is the shared financial capability, not a general microservices migration.

## Naming convention

Application names the backend host containing the product modules; it is not a separate Clean Architecture layer. Application.Core contains domain rules and use cases. Ledger remains independently deployed. This naming change does not change runtime ownership, operation identity or financial behavior. No code or live identifiers exist to migrate at this checkpoint.
