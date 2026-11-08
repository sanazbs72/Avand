---
name: avand-financial-reconciliation
description: Use when investigating or reviewing Avand discrepancies between gateway, bank or custodian evidence, Application state and Ledger records, including settlement timing, unmatched movements, suspense or corrections.
---

# Avand Financial Reconciliation

Trace a discrepancy from source evidence to its financial effect and operational owner. Preserve the original records and leave uncertainty visible until evidence supports closure.

## Find the applicable contract

Read the active feature specification, [Design section 13](../../../docs/DESIGN.md) and the [Constitution](../../../.specify/memory/constitution.md). Consult the relevant operation and suspense templates in [Financial Operations](../../../docs/FINANCIAL_OPERATIONS.md), account definitions and availability rules in [Chart of Accounts](../../../docs/CHART_OF_ACCOUNTS.md), and [Posting Examples](../../../docs/LEDGER_POSTING_EXAMPLES.md) for the expected accounting. Treat statement cadence, cutoff, tolerances, aging and escalation as approved feature or operational policy. Record a gap when the policy is missing.

## Compare evidence in both directions

1. Define the source, provider account, asset and as-of window. Record statement coverage and incompleteness. Read Application and Ledger records through their authorized interfaces. Compare records at compatible cutoffs or document known timing differences.
2. Match using stable scoped provider action references and the internal business or operation identity. Confirm amount or quantity, asset, direction and status. An amount and timestamp match alone cannot establish ownership or settle a case. Account for partial allocations, fees, refunds and batch netting using the applicable contract.
3. Find external actions without internal effects, internal effects without corresponding external evidence where required, duplicate actions, mismatches and pending items. Distinguish a delayed settlement from an unknown outcome using the available evidence and approved window. Missing evidence in an incomplete window cannot prove nonexecution.
4. For each discrepancy, record the evidence references, amounts, relevant Application and Ledger IDs, classification, owner, proposed next action and closure evidence. Keep sensitive payloads in their authorized evidence store.

## Choose the bounded next action

| Finding | Next action |
| --- | --- |
| Ledger Posted with Application still processing | Replay the existing outcome into Application using its original operation identity. Verify local convergence and exactly one required follow-up intent. |
| Verified provider fact with unresolved Ledger outcome | Recover the existing credit or settlement intent using its stable identity. Query the original outcome before assuming an internal effect is missing. |
| Verified physical movement with unknown ownership or purpose | Keep an authorized finance case. Use the approved unmatched-movement template when applicable. Keep suspense outside usable cash and eligible backing. |
| Expected cash settlement or gold receipt remains pending | Keep the receivable or pending item visible. Recognize cash or backing from verified receipt under the applicable contract. |
| Proven suspense ownership or erroneous posting | Use the approved typed resolution or linked correction with the required authority, evidence and financial guards. Preserve the original journal. |

Do not repair through direct balance edits, arbitrary journal legs or unexplained netting. Do not recredit a customer when identified cash settles an already credited capture. If a linked reversal cannot pass its own guards, retain the finance case for the approved resolution path. Separate recommendations from authorized execution.

## Verify convergence and closure

Check that rerunning the same statement or evidence produces the same matches and no additional financial effect. Exercise an unmatched receipt, a partial or net settlement, a stale Application state with Ledger Posted, and an internal-only item where relevant. Verify the resulting case, posting links and cash or backing classification. Close only with matched evidence, applied authorized disposition and verified final state. Report unresolved items and tests that did not run.
