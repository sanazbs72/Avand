# Avand project skills

Vendored from https://github.com/Aaronontheweb/dotnet-skills
Revision: 5ca5db8cf25566f19d2a5dc53204d4af22b23a0d
Installed: 2026-11-07
License: MIT; see LICENSE.dotnet-skills.

The five skill directories and their companion resources are unmodified upstream copies.
They are repository-scoped Codex skills, not a global plugin installation.
No NuGet dependencies, application features, or custom financial skills are installed by this change.

| Directory | Declared skill name |
| --- | --- |
| csharp-coding-standards | modern-csharp-coding-standards |
| csharp-concurrency-patterns | csharp-concurrency-patterns |
| efcore-patterns | efcore-patterns |
| testcontainers | testcontainers-integration-tests |
| cscheck | cscheck |

## Applicability to Avand

Project specifications and explicit architecture decisions govern implementation.
These skills provide general guidance; their examples do not approve dependencies or architecture.

- In-process synchronization, Channels and Actors do not establish cross-instance financial correctness. PostgreSQL transactions, constraints and the selected financial concurrency protocol remain authoritative.
- EF Core remains an implementation choice. Global NoTracking, SplitQuery, a separate migration host and blanket retry policies are not adopted merely by installing a skill.
- Retried financial writes require stable operation identity and recovery of unknown commit outcomes. Do not retry external financial effects blindly or hold a DB transaction across network calls.
- Records for entities and structs for every value object are not universal requirements. Select equality and persistence behavior deliberately, and compile examples against the actual SDK.
- Test financial persistence with real PostgreSQL. In-memory EF tests cannot prove transaction, locking or constraint behavior.
- Use project-approved, pinned package and container versions. Do not copy wildcard package versions, latest image tags or unrelated infrastructure from examples.
- Container cleanup must be restricted to resources owned by the test run; do not copy the upstream broad Docker prune example.
- CsCheck is available as guidance; no test framework or package selection is finalized by this installation.

## Updating

Review an explicit upstream revision, replace complete skill directories including resources,
retain the license, update this record and verify file integrity and local references.
Do not refresh the copies silently or assume all upstream snippets compile unchanged.

## PostgreSQL skill

Source: https://github.com/supabase/agent-skills
Revision: c9be0e931b7930f7d02126d04774d904c381e7d7
Installed: 2026-11-07
Directory and declared name: supabase-postgres-best-practices
License: MIT; see LICENSE.supabase-agent-skills.
The complete directory was installed with skill-installer from the pinned revision.
The ZIP download uses LF line endings; the inspected Git checkout uses CRLF.
No upstream content was edited.

### Applicability to Avand

This is general PostgreSQL guidance, not a complete financial correctness protocol.
It does not select the Supabase platform, Supabase Auth, RLS or database extensions for Avand.
Project specifications and explicit architecture decisions govern implementation.

- Financial integrity takes priority over performance rankings and illustrative speedup claims. No Avand benchmarks were run.
- Keep transactions short, but persist durable intent before external financial calls and recover unknown outcomes with stable operation identity. The short-transaction payment example alone does not provide this protocol.
- A multi-row UPDATE is atomic as a statement, but does not guarantee simultaneous lock acquisition or eliminate deadlocks. Use consistent lock ordering and handle retryable transaction failures.
- Advisory locks do not replace financial control-row locks or constraints. Session locks require care with pooling; use transaction-scoped locks only where the design calls for them.
- Use SKIP LOCKED for queue claims, not to skip locked balances or backing guards during financial admission. Workers still need durable crash recovery and idempotent effects.
- Mutable account balance examples do not authorize updates to posted journal entries. Projections must remain atomic with journal posting and durable operation results.

## Avand-specific skill

- `avand-ledger-correctness` covers the Ledger posting boundary, exact accounting, financial guards and verification of affected invariants. Its source of truth is the active feature contract and Constitution, with the Ledger design documents as supporting context.
- `avand-operation-recovery` covers durable admission, replay, unknown outcomes, forward repair and provider hold recovery. The active contract and Constitution govern its use.
- `avand-financial-verification` maps financial requirements to executable checks and records evidence at the appropriate verification boundary.
- `avand-architecture-boundary-review` traces service ownership, data access, dependency direction, module contracts and V1 scope.
- `avand-api-security-review` traces customer ownership, producer permissions, provider evidence, staff approval and sensitive data exposure.
- `avand-financial-reconciliation` classifies financial discrepancies, matches evidence and routes each case to an authorized resolution.
