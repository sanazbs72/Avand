# Quickstart — V01 validation

## Prerequisites and commands

Run from the repository root with the .NET 10 SDK and a Docker-compatible runtime. The integration fixture starts the pinned `postgres:17.6` image, creates isolated Application and Ledger databases and roles, applies both migrations, and starts both HTTP hosts with synthetic data. Check the toolchain with `dotnet --version` and `docker info`, then run:

```powershell
dotnet build Avand.slnx --ignore-failed-sources
dotnet test tests/Avand.V01.IntegrationTests/Avand.V01.IntegrationTests.csproj --no-build
```

The suite runs fixture-backed test classes serially to avoid concurrent PostgreSQL startup pressure on Docker Desktop. Concurrency and replay are still exercised inside their test cases. Use synthetic gateway fixtures and controlled time. Each fixture has isolated databases, credentials and starting state. For a demo, follow the [customer contract](contracts/v01.md) using the seeded identity and synthetic payment flow; retrieve result and balance separately. Fixture controls are test-only.

## Acceptance matrix

| Spec case | Exercise and observe |
| --- | --- |
| AC01 | Pay A with starting available B. Admission has zero financial effect. After Posted, status succeeds, balance is B+A, exactly one balanced IRR journal exists, and no trade or gold effect occurs. |
| AC02 | Overlap duplicate callback and inquiry for the same capture. Try changed amount and a different Ledger operation ID for the same action. Inspect one journal and one increment; conflicting evidence cannot create a new effect. |
| AC03 | Unauthenticated and financially ineligible initiation. Assert no gateway session or posting. |
| AC04 | Use a second customer to request the first customer's result; verify ownership and that each balance read resolves to its own principal. Probe the internal API with missing or wrong scope and database credentials with cross-store access attempts. |
| AC05 | Suppress callback. Matched definitive inquiry without original verification time leads to one Posted credit. |
| AC06 | Send late callback without previous verification and return definitive expiry; also exercise definitive expiry via inquiry. Assert Failed, zero credit, no refund dispatch. |
| AC07 | Inject timeout, ambiguous result, mismatched ID/amount and missing capture reference. Keep unresolved, with zero credit, and continue inquiry with the original reference. |
| AC08 | Drive unverified-payment inquiry through issuer-return pending and confirmed. Assert distinct messages, no Avand journal, cash debit or refund dispatch. |
| AC09 | Save verified evidence, delay first Ledger execution beyond 15 minutes, then recover. Assert pending and unchanged balance before commit; one credit afterward without repeat verification. |
| AC10 | Suppress Ledger response after proving commit. Restart Application and recover after the window under the same ID. Assert one journal, one increment and eventual successful status. |
| AC11 | Interrupt posting after writes begin but before commit. From a fresh connection assert zero partial lines, projections and terminal outcomes. Replay the same operation and observe one complete credit. |
| AC12 | Make restriction effective after admission and before first posting. Observe a single restricted credit, zero spendable delta and blocked new initiation. Repeat evidence; replay also preserves an earlier already-posted bucket. |
| AC13 | Use distinct valid sessions and a controlled clock immediately before, exactly at and immediately after 15 minutes. Observe acceptance, acceptance and definitive expiry, respectively. |

Use independent connections and synchronized contention for race tests. Record commands, exit codes, fault placement and durable observations in the [harness evidence format](../../docs/SDD_HARNESS.md) and [V01 evidence](../../harness/evidence/001-fund-irr-wallet.md). A build or static accounting fixture alone does not prove runtime behavior.
