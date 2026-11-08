# D025 - Use three backend projects with business modules inside Core

**Status:** Superseded historical folder proposal; replaced by D026 and revised PROJECT_STRUCTURE
**Design checkpoint:** 2026-11-06

[Decision log](../DECISIONS.md) | [Design](../DESIGN.md) | [Project structure](../PROJECT_STRUCTURE.md) | [Defense index](README.md)

## Decision

Historical proposal: three backend projects with Ledger split across shared Core and Infrastructure. The selected independent Ledger now has its own API, Core, Infrastructure and versioned Contracts alongside Application projects. PROJECT_STRUCTURE is the current layout.

## Why this fits V1

The earlier structure minimized project count. Its extraction cost became visible when Ledger was considered as the common financial service for three products.

## Alternatives considered

- One backend project: fewer initial project references, but weaker separation between HTTP, policy and infrastructure.
- Separate projects for every module and layer: stronger assembly boundaries, but more contracts and coordination overhead before the boundaries are proven.

## Tradeoffs and limits

Keep this record to explain the alternative, not as the active folder/DbContext plan. The current design has two deployable hosts and separately owned database contexts, without cross-service transaction sharing.

## Likely defense question

**Why add projects instead of keeping the original tree?**

The chosen runtime boundary needs explicit ownership of Ledger domain, API, persistence and contracts. Reusing one Core and DbContext would contradict that boundary. Project count is limited by combining Domain and UseCases within each service Core.

## Evidence to prepare

Use PROJECT_STRUCTURE and D026-D029 to trace current dependencies, database ownership and recovery behavior. No project scaffolding is claimed.

This is planned verification. No implementation or test result is claimed.

## When to revisit

Refine the selected layout from implementation evidence; do not mistake fewer folders for simpler runtime semantics.
