# Implementation Plan — V01 IRR wallet top-up and result

**Feature** — `001-fund-irr-wallet` · **Git branch** — `main` · **Date** — 2026-11-07 · [Specification](spec.md)

## Summary

Deliver authenticated top-up initiation, provider verification or inquiry, one durable Ledger credit and owner-scoped result and spendable balance reads. Preserve provider and Ledger outcomes independently. Q1, Q11 and Q12 plus C01–C03 govern the design.

## Technical Context

- **Language and dependencies** — C# / .NET 10, existing ASP.NET Core hosts, EF Core 10 and compatible Npgsql provider, typed HTTP client, hosted background processing.
- **Storage and platform** — PostgreSQL 17, two logical databases with independent credentials and migrations. Local .NET SDK and Docker-compatible container runtime for verification.
- **Testing** — xUnit, Testcontainers PostgreSQL, both service hosts, deterministic gateway clock and controlled faults. Run the 13 acceptance scenarios and verify durable state.
- **Performance and scope** — One demo slice, with no production throughput or latency target specified. Apply bounded request timeouts and persistent backoff, keeping unknown results recoverable. The 15-minute boundary governs first provider verification, not processing latency.

## Constitution Check

| Gate | Phase 0 assessment | Phase 1 design check |
| --- | --- | --- |
| I–II Traceability and scope | V01 only | [Quickstart](quickstart.md) maps AC01–AC13. No trading, settlement, withdrawal or post-capture return implementation. |
| III–IV Ownership and contracts | Two existing hosts | [Contracts](contracts/v01.md) keep customer workflow in Application and financial decisions and reads in Ledger. |
| V–VI Financial integrity | Exact IRR and one posting | [Model](data-model.md) records protected eligibility, journal, synchronous projection and result as one decision. |
| VII–X Recovery, eligibility and external facts | Independent local transactions | Immutable intent and same-operation recovery preserve prior facts. No provider call inside a DB transaction. Restricted funds and receivables remain separately classified. |
| XI–XIII Verification, security and governance | Evidence and ownership required | Test matrix includes unauthorized reads, provider uncertainty, races, rollback and post-commit response loss. Runtime proof remains pending implementation. |

Design gates pass. No constitutional exception is required.

## Project Structure

- Existing `src/Avand.Application.Api/` hosts customer endpoints, payment use cases, gateway and Ledger adapters, local persistence and recovery.
- Existing `src/Avand.Ledger.Api/` hosts scoped internal contracts, eligibility and posting use cases, and its own persistence.
- Within each host, keep transport and persistence behind use-case boundaries. Do not add a runtime project reference between hosts.
- Planned `tests/Avand.V01.IntegrationTests/` hosts the cross-service acceptance suite and synthetic fixtures.
- [Research](research.md), [data model](data-model.md), [contracts](contracts/v01.md), and [quickstart](quickstart.md) comprise this design.

## Implementation sequence

1. Establish service identities, per-service migrations and fixtures, deterministic gateway and clock, and the two-host verification harness.
2. Implement Ledger C04 and F02, durable decision lookup and authoritative balances. Prove replay, ownership, atomic posting, and restriction ordering before wiring dispatch.
3. Wire Application initiation, callback and inquiry into persistent provider and Ledger work. Apply durable results forward and expose owned reads. Keep customer display semantics from the contract.
4. Validate AC01–AC13, inspect durable evidence across service boundaries and record unrun or blocked cases separately.

## Complexity Tracking

No exceptions.
