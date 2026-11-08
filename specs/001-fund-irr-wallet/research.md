# Phase 0 Research — V01

## R1. Two hosts and two stores

**Decision** — Extend the existing .NET 10 ASP.NET Core hosts. Use EF Core 10 with the compatible Npgsql provider and separate migrations, database owners and runtime credentials. Use one PostgreSQL 17 instance with two databases for the demo and tests. Pin compatible package patches and the test image in implementation.

**Rationale** — The project files already target net10.0. The constitution assigns separate persistence ownership. A single demo instance lowers setup cost while preserving credentials and database boundaries.

**Alternatives considered** — One shared database crosses ownership. Separate engines are allowed but unnecessary for this demo. A new project per layer adds setup without changing ownership. [Repository structure](../../docs/PROJECT_STRUCTURE.md) · [EF Core transactions](https://learn.microsoft.com/en-us/ef/core/saving/transactions)

## R2. Provider evidence and time

**Decision** — Use a deterministic synthetic gateway, stable payment references and a controllable clock. A callback schedules verification or inquiry for the same payment. A fresh verify uses the gateway session timestamp and accepts elapsed time ≤15 minutes. Matched, definitive captured evidence with unique capture reference and exact IRR amount is sufficient, including inquiry without an initial verification timestamp.

**Rationale** — C01 and C02 distinguish verification time from transport and Ledger delivery. Keep provider certainty and posting certainty separate.

**Alternatives considered** — Callback timestamp, arrival time, and an Application deadline cannot establish provider rejection. Requiring an initial verification timestamp in inquiry contradicts C02. [Specification](spec.md) · [Company answers](../../docs/COMPANY_CLARIFICATIONS.md) · [Payment design](../../docs/DESIGN.md#10-independent-wallet-top-up)

## R3. Durable recovery and atomic posting

**Decision** — Use a persisted Application work record and a hosted polling worker for provider inquiry and Ledger recovery. Persist matched capture and immutable F02 intent together. Reuse the original operation identity across retries and read back committed results. Use a Ledger-local explicit transaction at Read Committed with unique claims and deterministic row locking on effective eligibility, payment state and affected accounts. Seed the control and account rows required for the demo. Use exact integer IRR with checked arithmetic.

**Rationale** — Unique claims close races between concurrent first attempts. The eligibility lock serializes C02 control changes against first posting, so the bucket is chosen from effective state at that decision. A later restriction cannot change a stored outcome. Commit-time protection and local rollback preserve balance and recovery consistency.

**Alternatives considered** — In-memory queues lose work on restart. A broker is unnecessary for V01. Read-then-write without protected claims and locks admits races. A distributed transaction crosses the service boundary. [Ledger](../../docs/LEDGER_SERVICE.md#4-one-atomic-ledger-transaction) · [PostgreSQL row locks](https://www.postgresql.org/docs/17/explicit-locking.html)

## R4. Validation and demo entry point

**Decision** — Use the Application API as the customer-facing demo surface with seeded authenticated identities and synthetic gateway controls. Use xUnit and Testcontainers against PostgreSQL with both HTTP hosts, explicit clock and fault controls, and owner-scoped test readback. Keep fixture controls outside deployed customer routes.

**Rationale** — Duplicate, rollback and lost-response claims require actual persistence and both logical service boundaries. Separate test cases distinguish a committed posting from an interrupted one.

**Alternatives considered** — Mock-only tests cannot establish durable effects. A separate browser app is unnecessary for the API demonstration. Production identity integration and gateway credentials are outside the demo. [Harness](../../docs/SDD_HARNESS.md) · [PostgreSQL Testcontainers](https://dotnet.testcontainers.org/modules/postgres/)
