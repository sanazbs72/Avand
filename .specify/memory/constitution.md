<!--
Sync Impact Report
Version change: 1.0.0 (Avand source) to 1.0.1 (AvandTask adoption).
Modified principles: I–XII retain their titles and rules under the resolved template.
  XIII. Completion and governance moved under Governance. Semantic versioning
  and compliance review wording were made explicit.
Added sections: Core Principles, Verification, Security and Operations, Governance.
Removed sections: None. The destination previously contained the unratified scaffold.
Compatibility checked: Spec Kit plan template, Specify, Plan, Tasks, Analyze,
  and the repository guidance in AGENTS.md.
Follow-up TODOs: None.
-->

# Avand Development Constitution

Method: Specification-Driven Development (Spec Kit)
Stack: C# / .NET 10 and PostgreSQL
Scope: Engineering baseline derived from the agreed Avand design.

MUST, MUST NOT and SHOULD express requirements, prohibitions and recommendations. This document governs repository engineering decisions; it does not override explicit user instructions or the agent's higher-priority safety and execution rules.

## Core Principles

### I. Specification and decision traceability

Each feature MUST have a reviewable specification covering behavior, invariants, failure/unknown outcomes and acceptance criteria before its affected implementation begins. Plans, tasks, code and tests MUST trace to that specification.

Confirmed decisions MUST be distinguished from proposals and superseded alternatives. An unapproved proposal MUST NOT silently become a business rule. Resolve material uncertainty before implementing the affected behavior; continue independent authorized work.

Behavioral changes MUST update the applicable specification, contract and acceptance criteria. Historical defense documents explain decisions; they are not a second requirements authority.

### II. Scope and delivery

Baseline V1 implements Gold trading and its required customer, payment, withdrawal, ledger, custody/recovery, support and reconciliation capabilities.

Fund and Card MUST remain design-only until explicitly brought into implementation scope. Their design documents MUST NOT trigger speculative endpoints, migrations, workers or dependency installation.

Prefer thin end-to-end slices and evidence of correctness over breadth. No production-readiness, regulatory approval, experience or performance result may be invented.

### III. Service and module ownership

Application and Ledger are separately deployed backends with separately owned PostgreSQL databases and credentials. A shared PostgreSQL instance does not permit shared tables, contexts, transactions or direct cross-service SQL.

Application owns product intent, orders, customer profile, provider integrations and operational workflows. Ledger exclusively owns financial accounts, journals, authoritative balances, financial holds/controls and durable financial outcomes.

Within Application, product capabilities MUST have explicit logical ownership and narrow contracts. GoldTrading, Fund and Card MUST NOT share one overloaded product-order entity or write one another's private state.

The exact module assembly/project layout is a plan decision, not fixed by this constitution. Separate deployment is not required for every module.

### IV. Dependency direction and contracts

Business rules MUST remain independent of HTTP, ORM, broker and provider implementation details. Use cases coordinate rules through narrow ports; Infrastructure implements those ports.

API handlers MUST remain transport/authentication/mapping adapters. Infrastructure references in the composition root are permitted; endpoints MUST NOT directly post balances or bypass use cases.

Ledger wire contracts MUST be versioned DTOs, not shared domain entities, DbContext or repositories. Internal domain events need not have a separate assembly or external publication. Integration contracts MUST have explicit ownership and compatibility rules.

### V. Financial integrity

Every journal MUST balance Debit and Credit separately for each asset. Exact asset units, normal-side conventions and rounding MUST follow the approved account/operation specification.

Financial amounts MUST NOT use float/double. Intermediate arithmetic, aggregates and final ranges MUST be checked using sufficiently wide exact types.

Customer spendable balances MUST NOT become negative. Signed internal control accounts require explicit account-specific rules.

Journals MUST be append-only. Corrections MUST use authorized linked new postings, never destructive updates/deletes. Suspense MUST remain visible, evidenced and excluded from usable cash/backing.

### VI. Atomic Ledger execution and projections

All financial legs, fees, balance projections, relevant holds/control changes, deduplication claims and durable operation outcome MUST commit in one Ledger-local transaction.

The authoritative spendable balance projection MUST update synchronously with entries and remain reconstructable from them. No independent balance-edit path is allowed.

Funding and shared-capacity checks MUST be protected against concurrent changes. A transaction around writes alone does not protect an earlier unguarded read.

Repository abstractions MAY represent aggregate persistence where useful. Generic CRUD repositories, repositories per table, a separate Unit of Work wrapper and transaction decorators are not mandatory. No repository may commit independently. Ledger posting requires an explicit controlled financial-operation boundary.

### VII. Local transactions and durable coordination

No database transaction may span an HTTP/provider call, broker delivery or another service's database.

Application MUST persist immutable financial intent and its permanent OperationId before dispatch. Ledger financial finality MUST be recovered through its durable outcome. Application applies that result and creates required follow-up intent atomically in its own database.

A Product/Application status failure MUST NOT automatically reverse an otherwise completed financial trade. Repair knowledge forward.

A transaction decorator MAY wrap an appropriate local persistence phase; it MUST NOT blindly wrap an entire network-orchestrating handler.

### VIII. Idempotency, unknown results and holds

API retry identity, business-action identity and provider-action identity MUST be protected durably at their appropriate boundaries. Same identity with changed economic content MUST conflict.

Retry MUST preserve identity and immutable economic data. Timeout, missing callback and NotFound MUST NOT alone establish nonexecution.

Holds MUST have stable identities and explicit remaining entitlement/exposure. Capture, release, partial settlement and conflicting outcomes MUST serialize. Potentially dispatched external work MUST NOT lose its protection merely because time elapsed.

Replay returns the original durable decision, not a newly calculated price or current-state result.

### IX. Cash, backing and eligibility

Pending purchases, receivables, expected funding and suspense MUST NOT count as confirmed usable cash or eligible physical backing.

Cash commitments and payables MUST be counted once. All products MUST use the same authoritative financial capacity, not independent spendable copies.

Financial eligibility/restrictions MUST have an explicit effective-time/version contract. Restrictions block new instructions but MUST NOT erase real earlier financial facts. Funds received under restriction follow an explicit approved bucket/release policy.

Numeric caps, TTLs, fee rates, calendars and withdrawal limits belong in approved feature contracts/configuration, not hardcoded constitutional articles. Changes MUST be traceable and testable.

### X. External effects and messaging

External clients MUST be Infrastructure adapters that translate provider models/errors into internal concepts. Timeouts, cancellation, evidence and retry semantics MUST be explicit.

Whenever durable follow-up intent/outbox records are required, they MUST commit with the owning service's initiating state. Never rely on fire-and-forget work for required financial effects.

No broker or Saga framework is mandated for V1. If messages are introduced, assume redelivery, use durable producer intent and idempotent consumers, and do not claim exactly-once external execution.

## Verification

### XI. Verification proportional to financial risk

Money-changing paths MUST have tests for relevant invariants, rejection, duplicates, concurrency, crash/rollback and unknown-result recovery.

PostgreSQL behavior MUST be tested against real PostgreSQL. Service coordination MUST be tested across the real logical service boundary. Mock-only tests cannot prove SQL locking or cross-service recovery.

Tests MUST check observable behavior and actual failure modes, not simply duplicate implementation. No blanket coverage percentage or test project per folder is required.

Machine-checkable dependency, ownership and posting constraints SHOULD have automated enforcement. Static journal examples are useful evidence but not application test results.

## Security and Operations

### XII. Security, audit and operations

Resolve customer ownership from validated identity/authorization context. Never trust a body customer/actor ID as access authority.

Restrict producer operation scopes, financial administrative actions and database privileges. Approval references require verified authority, not arbitrary IDs.

Do not expose secrets, credentials, raw card security data or unnecessary customer identifiers in logs, fixtures, commits or AI context. Use synthetic external mocks for the exercise.

Preserve correlation among customer intent, Ledger operation, journals and provider evidence. Relevant changes MUST include actionable failure visibility and recovery ownership.

## Governance

### XIII. Completion and governance

A feature is complete only when applicable acceptance criteria, checks and review are satisfied with observed evidence. Record commands, outcomes and unverified limitations; do not infer success from generated code.

Maintain one active Spec Kit specification/plan/task source per feature. Skills and harness logs MUST NOT create competing implementation plans.

Material changes to financial invariants, ownership, scope or public contracts require a recorded decision and impact assessment. Routine reversible implementation choices may proceed within the authorized specification.

Amendments MUST version this constitution, state the reason and identify affected specifications/templates. Spec Kit-generated templates and agent context MUST be checked for conflict; do not claim synchronization when the tooling is not installed or has not run.

Use a major version for incompatible changes to governance or principles, a minor
version for added principles or materially expanded guidance, and a patch version
for wording, formatting, or other changes that preserve the rules.

Feature reviews MUST check the applicable constitutional rules against the active
specification, plan, tasks, and observed verification evidence before completion.

**Version**: 1.0.1 | **Ratified**: 2026-11-06 | **Last Amended**: 2026-11-07
