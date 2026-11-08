# D029 - Bound the service split to the available team and assignment time

**Status:** Delivery proposal under the confirmed service direction
**Design checkpoint:** 2026-11-06

[Decision log](../DECISIONS.md) | [Design](../DESIGN.md) | [Ledger service](../LEDGER_SERVICE.md) | [Defense index](README.md)

## Decision

Build at most the Application API and Ledger API deployments for baseline V1, with one repository/stack, owned databases, hosted recovery workers and no required broker or Saga engine. Keep Fund/Card design-only and treasury manual.

## Why this fits V1

The brief starts with one backend-capable lead, one frontend engineer and a designer and allows 72 hours total. Scope control must pay for the chosen financial service boundary.

## Alternatives considered

- A deployment for every module: adds delivery and operational surfaces without the corresponding team.
- Broad infrastructure before the financial failure slice: consumes time without proving money correctness.
- Assume frontend/design staff can parallelize backend work: contradicts the stated initial roles.

## Tradeoffs and limits

The lead remains a backend bottleneck. Actual build time and latency are unknown. The scope is a prioritization proposal, not proof the complete brief fits or a replacement for the required 12-week plan.

## Likely defense question

**What gets built first, and what is cut when time is tight?**

First prove durable admission, atomic posting, lost-response recovery and concurrency on the real two-service/database boundary. Then extend the product flows. Cut polish and optional infrastructure before financial invariants, and state remaining omissions honestly.

## Evidence to prepare

Demonstrate both hosts and owned databases with one local command, plus the correctness/failure tests. Show support visibility into unresolved operations instead of masking the second service's failure modes.

This is planned verification. No implementation or test result is claimed.

## When to revisit

Estimate remaining scope from the first proven slice and actual remaining time. Add infrastructure or independent deployments only for evidence-backed needs.

## Contract completeness is not a delivery estimate

D030-D033 add a complete draft accounting/API vocabulary. Twenty-one typed financial templates do not mean twenty-one services, but they still require implementation effort. Prioritize the required core acceptance scenarios and recovery; disclose unsupported advanced automation. Do not return Posted for an unimplemented type. The design can be complete for an operation before its implementation is delivered.
