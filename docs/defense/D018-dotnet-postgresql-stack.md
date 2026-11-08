# D018 - Use C# on .NET 10 and PostgreSQL

**Status:** Confirmed stack; EF Core 10 and Npgsql 10 proposed
**Design checkpoint:** 2026-11-06

[Decision log](../DECISIONS.md) | [Design](../DESIGN.md) | [Defense index](README.md)

## Decision

Use C# and .NET 10 for the backend and PostgreSQL for persistence. The proposed access stack is ASP.NET Core 10, EF Core 10 and Npgsql 10.

## Why this fits V1

These are the customer's chosen implementation technologies. PostgreSQL supports the relational constraints and transaction controls required by the design.

## Alternatives considered

- Other languages or databases: no demonstrated benefit outweighing the chosen stack.
- Additional persistence engines for V1 balances: adds synchronization and operations work.

## Tradeoffs and limits

Exact database and package patch versions, deployment image and migrations need pinning during implementation.

## Likely defense question

**Why this stack rather than a specialist ledger service?**

It matches the selected engineering stack and keeps the core behavior inspectable in-house. This is not a claim of personal production experience that has not been supplied.

## Evidence to prepare

Build against the pinned toolchain and run financial concurrency tests against the selected PostgreSQL version.

This is planned verification. No implementation or test result is claimed.

## When to revisit

Change only for a demonstrated hiring, operational or contractual constraint.

## Independent-service implications

Use the chosen stack for two backend hosts, with owned Application/Ledger databases. EF Core/Npgsql and exact versions remain proposed implementation choices. Sharing one PostgreSQL instance for the exercise is not a shared transactional model.
