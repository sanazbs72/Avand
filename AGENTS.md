# Avand repository guidance

## Start with the feature

- Read the [Constitution](.specify/memory/constitution.md) and the active Spec Kit feature's `spec.md`, `plan.md` and `tasks.md` where available. Identify the task and its acceptance criteria before changing code.
- For affected Gold V1 behavior, read the [company clarifications](docs/COMPANY_CLARIFICATIONS.md) alongside the active contract. Apply the confirmed Q1–Q17 requirements where they supersede earlier assumptions. If the active feature artifacts disagree, reconcile the affected specification and acceptance criteria before implementing that behavior. Route unresolved design interpretations through Clarify.
- Use the [decision log](docs/DECISIONS.md) to distinguish confirmed decisions, working proposals and superseded choices. Open the relevant contracts under `docs/` before asking for clarification.
- Run a [Clarify pass](docs/SDD_HARNESS.md#51-clarify-before-plan) after Specify and before Plan. Resolve open questions that change scope, financial behavior, service ownership, API contracts or acceptance criteria. Record the answers in the active `spec.md` and defer planning or implementation that depends on unanswered questions. Continue independent authorized work.
- Keep feature scope, plan and task status in the active Spec Kit artifacts. Use the [Harness guide](docs/SDD_HARNESS.md) for verification and evidence conventions.

## Analyze before Implement

- After Tasks, run [Analyze](docs/SDD_HARNESS.md#52-analyze-before-implement) across the Constitution, `spec.md`, `plan.md` and `tasks.md`. Check for contradictions, missing acceptance coverage and incorrect task ordering. Verify that affected tasks reflect the company-confirmed requirements and approved interpretations. Resolve findings that block the affected implementation in the owning artifact, then rerun Analyze.
- In [Implement](docs/SDD_HARNESS.md#53-implement-the-validated-tasks), follow the analyzed task order, load the relevant skills and implement one authorized slice at a time. Run the applicable checks and record evidence before marking a task complete. Return to Clarify if implementation exposes a material requirement gap.

## Preserve the service boundaries

- Application API owns product intent and workflow. Ledger API owns financial decisions and records. Each service uses its own database credentials and reaches the other through its published contract.
- Gold and its supporting flows are in V1 implementation scope. Fund and Card remain design-only until explicitly brought into scope.
- Use synthetic provider and customer data in tests and evidence. Keep credentials and sensitive payloads out of logs and shared artifacts.

## Load the skills relevant to the task

Read the linked `SKILL.md` when a change reaches that surface. A task can require more than one skill.

| Task touches | Skill |
| --- | --- |
| Journal, spendable balance, fee, hold, backing or liquidity | [avand-ledger-correctness](.agents/skills/avand-ledger-correctness/SKILL.md) |
| Idempotency, retry, lost response or provider outcome | [avand-operation-recovery](.agents/skills/avand-operation-recovery/SKILL.md) |
| Financial acceptance criteria, database races or verification evidence | [avand-financial-verification](.agents/skills/avand-financial-verification/SKILL.md) |
| Service ownership, module dependencies or V1 scope | [avand-architecture-boundary-review](.agents/skills/avand-architecture-boundary-review/SKILL.md) |
| Customer access, service scopes, callbacks or approvals | [avand-api-security-review](.agents/skills/avand-api-security-review/SKILL.md) |
| Differences between external evidence and internal records | [avand-financial-reconciliation](.agents/skills/avand-financial-reconciliation/SKILL.md) |
| PostgreSQL schema, queries, transactions or locking | [supabase-postgres-best-practices](.agents/skills/supabase-postgres-best-practices/SKILL.md) |

Use the [remaining project skills](.agents/skills/README.md) when the task calls for C#, EF Core, Testcontainers or property-based testing. Apply general examples within Avand's approved contracts and dependencies.

## Verify and record the result

- For .NET changes, build with `dotnet build Avand.slnx --ignore-failed-sources`. Run the focused tests that exist for the task. Use real PostgreSQL to verify constraints and concurrency. Verify recovery across both logical services when the change crosses that boundary.
- Use `eng/verify.ps1` once it exists and runs the applicable checks. Until then, run the actual build and test commands directly.
- Record feature evidence in `harness/evidence/<feature>.md` when verification is performed. Include the feature or task ID, command, exit code, observed result and outstanding checks. Mark missing or unexecuted checks `NotRun` or `Blocked` as appropriate.
- Update the original Spec Kit task using the observed evidence. Keep static posting fixture results separate from runtime results.
