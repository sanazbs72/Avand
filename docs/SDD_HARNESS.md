# Avand - SDD Harness Recommendation

Date: 2026-11-06
Status: Recommended execution design. Only this document and the constitution are authored; no agent instruction, CI workflow, scripts, skills or application scaffold are installed.

[Constitution](../.specify/memory/constitution.md) | [Design](DESIGN.md) | [Decision log](DECISIONS.md)

## 1. What the harness adds

Spec Kit owns feature requirements, clarification, plan and tasks. The harness adds durable repository context, reliable executable checks and an evidence trail around implementation. It is not another planner, autonomous runtime or service.

Use the existing coding-agent runtime. Do not build a new agent SDK/orchestration system to implement Avand.

## 2. Minimal proposed layout

```text
.specify/memory/constitution.md       engineering principles
specs/<feature>/spec.md              Spec Kit requirements
specs/<feature>/plan.md              Spec Kit technical plan
specs/<feature>/tasks.md             one active task list
AGENTS.md                           short repository navigation and working rules
eng/verify.ps1                      repeatable local check entry point
harness/evidence/<feature>.md        observed commands/results and limitations
.agents/skills/                      selected agent skills, if using Codex
docs/                               contracts, decisions and defense
```

The Constitution, design documents, `AGENTS.md` and project skills are available in the current workspace. The feature artifacts, verification entry point and evidence files are created as work requires them. The installed Spec Kit integration determines its generated layout and invocation syntax. Keep one active plan and task list per feature in Spec Kit.

## 3. AGENTS.md should be short

Recommend including:
- Read the constitution and current feature artifacts.
- Run Clarify before planning work that depends on unresolved requirements.
- Use docs/DECISIONS status: confirmed versus proposed versus superseded.
- Open only the relevant account, operation and product contracts.
- Application/Ledger ownership and V1 design-only Fund/Card boundary.
- Verified build/test commands once real projects exist.
- How to run focused checks and record evidence.
- Required secret/data handling.

Do not paste every skill or the full architecture into AGENTS.md. Do not invent commands or mark placeholders as successful checks. Preserve explicitly authorized scope and continue routine work without extra approval loops.

## 4. Selective skills

- software-architecture-design: planning and ownership/contract changes.
- dotnet-csharp-engineer: implementation and debugging of .NET paths.
- ddd-tdd-bdd-dotnet: financial domain behavior and meaningful tests.
- event-driven-systems: durable intent, idempotency, inquiry and external recovery.
- dotnet-code-review: review changed financial/persistence/API paths.
- refactoring-safely: only when restructuring existing behavior.
- gherkin-business-test-writer: only if executable Gherkin is actually selected.

Do not run every skill for every task. Avoid a second spec/plan/task workflow alongside Spec Kit. Existing personal/global skills do not automatically exist in a fresh agent environment; install/copy only the needed reviewed skills when that environment is selected.

## 5. Execution loop

1. **Specify.** Define one bounded feature with observable acceptance criteria.
2. **Clarify.** Resolve material ambiguities and update the active specification before planning the affected work.
3. **Plan.** Map the clarified requirements and invariants to the approved contracts and verification approach.
4. **Tasks.** Keep tasks small, ordered and traceable to acceptance criteria.
5. **Analyze.** Check consistency and coverage across the Constitution, specification, plan and tasks. Resolve blocking findings before implementing the affected work.
6. **Implement.** Follow the analyzed task order and work on one authorized slice at a time.
7. **Verify and review.** Run focused checks, then the affected integration and recovery checks. Fix findings and verify the fixes.
8. **Record evidence.** Capture observed results and update the original Spec Kit tasks. Reconcile implementation with the spec before completion.

Follow the installed Spec Kit version's actual commands. Do not assume this repository already has specify installed or claim its templates were synchronized.

### 5.1 Clarify before Plan

Use the active `spec.md`, Constitution, decision log and relevant contracts as input. First resolve what the existing sources already answer. If an unresolved choice changes scope, money movement, eligibility, provider finality, failure or retry behavior, service ownership, API contracts or an acceptance criterion, ask a focused question and explain which behavior depends on the answer.

Write the accepted answer and any affected acceptance criteria in the active `spec.md`. Record a change to an agreed decision in the decision log. When all material questions are resolved, continue to Plan. If a question remains open, record it with the affected work, defer dependent planning and implementation, and continue independent authorized work. Routine implementation choices within the approved behavior belong in Plan.

For example, the 1% backing rule is confirmed for a custodian outage. If a feature also needs a healthy-mode rule, Clarify must establish that behavior before the healthy-mode work is planned. If a material ambiguity surfaces later, return to Clarify for that decision and update the affected spec, plan and tasks before continuing.

### 5.2 Analyze before Implement

Use Analyze as a read-only pass after Tasks. Compare the Constitution, active `spec.md`, `plan.md` and `tasks.md`. Check that every applicable acceptance criterion has a task and a verification path, that tasks follow their dependencies, and that the plan and tasks preserve the approved scope, financial invariants and ownership boundaries.

Record findings with the affected artifact, requirement and task. Fix blocking findings in the owning artifact and rerun Analyze before starting the affected implementation. Use Clarify for unresolved behavior, Plan for technical inconsistencies, and Tasks for missing coverage or ordering. For example, if the spec requires recovery after a lost Ledger response but the tasks only test success, add the recovery verification to the existing task list before implementing that flow.

### 5.3 Implement the validated tasks

Follow the active `tasks.md` in dependency order. For each authorized slice, read its acceptance criteria and load the relevant skills through `AGENTS.md`. Observe meaningful failing behavior before changing financial paths where practical. Implement the slice and run its applicable checks. Record observed evidence before marking the task complete.

If implementation reveals an unresolved requirement or changes the plan or task dependencies materially, update the owning artifacts and rerun Analyze for the affected work before continuing.

## 6. Executable verification gates

Once code exists, eng/verify.ps1 should orchestrate actual pinned commands and return nonzero on failure:
- Build/type checks.
- Domain calculations and posting invariants.
- Real PostgreSQL migrations, constraints, atomic projection and concurrency.
- Two-service response-loss/restart recovery.
- API authentication, ownership and stable replay.
- Original-to-adapted acceptance scenario mapping.
- Dependency rules and database privilege boundaries.
- Secret/sensitive fixture checks appropriate to the repository.

Keep deterministic state/reset, test clocks and controlled provider modes. A missing required test or unavailable dependency is NotRun/Blocked, never Pass. Do not create a green verification stub before the underlying checks exist. CI runs the same entry point rather than a second unrelated validation definition.

## 7. Evidence format

Record feature/task IDs, source revision or patch identity when available, commands, exit codes, concise results, review findings and unverified paths. Separate static accounting-fixture checks from runtime/PostgreSQL checks.

Do not fabricate timestamps, commits, benchmarks or personal incidents. Keep evidence concise; reference logs instead of copying secrets or long output.

## 8. First slices and scope

Recommended order, to be turned into Spec Kit feature artifacts rather than a second task list:
- Ledger kernel: account provisioning, balanced posting, projections, operation identity.
- Financial guards and concurrent acceptance.
- Application-to-Ledger durable intent and lost-response recovery.
- Independent top-up and inquiry.
- Gold quote/buy/sell, then withdrawal.
- Reconciliation and support/demo evidence.

Do not scaffold Fund/Card code from future-flow documents. Preserve business choices still marked Proposed until resolved for the affected slice.

## 9. Official references and interpretation

- [Spec Kit workflow](https://github.com/github/spec-kit/blob/main/docs/reference/agentic-sdd.md).
- [OpenAI: iterating development workflows](https://developers.openai.com/cookbook/examples/codex/iterating-development-workflows-with-codex).
- [OpenAI: repository skills and CI workflows](https://developers.openai.com/blog/skills-agents-sdk).

This is a project-specific adaptation: Spec Kit remains the sole planner, while optional harness conventions supply context, checks and evidence. These file names are not all mandatory Codex runtime files.
