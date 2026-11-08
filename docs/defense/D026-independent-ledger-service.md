# D026 - Deploy Ledger independently as the shared financial capability

**Status:** Confirmed direction; protocol and project details are working design
**Design checkpoint:** 2026-11-06

[Decision log](../DECISIONS.md) | [Design](../DESIGN.md) | [Ledger service](../LEDGER_SERVICE.md) | [Defense index](README.md)

## Decision

Use two backend deployments: a modular Application API and an independent Ledger API with a separately owned PostgreSQL database. Gold, future Fund and future Card use the same authoritative financial capability.

## Why this fits V1

Ledger is a platform boundary across three products, not an implementation detail of Gold Trading. Independent ownership and release make that role explicit. The user chose this direction after reviewing the simpler in-process alternative.

## Alternatives considered

- One modular application/database: smallest initial coordination cost, but does not provide independent Ledger deployment.
- An independently packaged Ledger inside the same deployment: strong code ownership with lower runtime cost; a valid alternative deliberately not selected.
- A service per product/module: unnecessary operational scope for the initial team.

## Tradeoffs and limits

There is one additional backend host, database boundary, internal API and recovery workflow. Ledger downtime stops new financial execution. One PostgreSQL instance can host both databases for the exercise, so engine-level failure isolation is not claimed.

## Likely defense question

**Why accept a second service with only 72 hours and a small team?**

The team starts with one backend-capable lead, one frontend engineer and a designer. I therefore restrict the split to two hosts, use the same .NET/PostgreSQL stack and repository, and keep coordination to durable operation inquiry/retry. Fund and Card remain design-only. The price of this choice is real recovery and integration work, not a promise of free microservices.

## Evidence to prepare

Show one atomic multi-asset operation, lost response after commit, one durable result on retry, protected spending across product-shaped callers, and database ownership separation.

This is planned verification. No implementation or test result is claimed.

## When to revisit

Reassess from measured recovery/latency and delivery evidence. Do not add more services merely because a module exists.

## Resource-aware defense

The brief allows 72 hours total, not 72 hours for the Ledger split alone. Current elapsed time is unknown, so there is no invented delivery estimate.

The selected scope adds one service boundary rather than decomposing the entire product. Both hosts reuse C#, .NET 10, PostgreSQL, one repository, shared build configuration and a small versioned DTO contract. A local PostgreSQL instance holds two owned databases; Application never accesses Ledger tables.

The lead handles Ledger, financial controls, Application coordination and correctness tests. The senior frontend engineer builds the demo and support views against explicit status contracts. The designer makes Processing, Unknown and review states understandable. They are not three interchangeable backend engineers.

No required broker, Saga engine, service mesh, distributed transaction or general workflow platform is added. The first engineering slice must demonstrate the recovery failure path as well as success before broadening functionality.

If this recovery cost consumes the available time, record the resulting omissions honestly. Reduce cosmetic/optional scope before weakening posting, idempotency or recovery. Do not claim this architecture guarantees the whole assignment will fit.

## What this decision does not claim

An independent service does not by itself make the Ledger scalable, highly available or regulation-compliant. It also does not make every product failure independent: all financial execution still depends on Ledger. The journal protocol and operations evidence must earn those claims.

## Suggested spoken explanation

"I treated Ledger as the common financial capability for Gold, Fund and Card. I accepted one service boundary, not a service per product. All financial legs commit together inside Ledger. Application state recovers using a permanent OperationId, so a lost response cannot create a second trade. For the initial team, I kept one repository and stack and left treasury manual, with no broker or Saga framework required for this V1 slice."
