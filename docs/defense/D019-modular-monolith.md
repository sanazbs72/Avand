# D019 - Use a modular monolith and one transactional database for V1

**Status:** Superseded historical proposal; replaced by D026
**Design checkpoint:** 2026-11-06

[Decision log](../DECISIONS.md) | [Design](../DESIGN.md) | [Defense index](README.md)

## Decision

Historical proposal: use one modular application and one transactional database. The user subsequently chose an independent Ledger API alongside a modular Application API. D026 is the current deployment decision.

## Why this fits V1

The original reason was the initial team and short deadline. It remains a valid alternative to explain, but it is no longer the selected architecture.

## Alternatives considered

- An undifferentiated application: slightly less initial structure but weaker ownership.
- Microservices: adds distributed contracts and partial failure before independent deployment is required.

## Tradeoffs and limits

Do not implement the historical single-context/single-deployment boundary. The selected architecture pays for one service boundary with durable operation recovery and restricts V1 to two hosts.

## Likely defense question

**Why did you change the original recommendation?**

The discussion elevated Ledger to the shared financial capability for Gold, Fund and Card. The user selected independent deployment after reviewing a bounded atomic-operation/recovery protocol rather than assuming a full Saga platform was required.

## Evidence to prepare

Use D026-D029 and LEDGER_SERVICE for the current architecture and resource tradeoffs. Do not present the historical local Application/Ledger transaction as implemented behavior.

This is planned verification. No implementation or test result is claimed.

## When to revisit

Retain this record as the considered alternative. Any later topology change needs an explicit decision and evidence.
