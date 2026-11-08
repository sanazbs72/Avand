# V01 IRR wallet top-up verification evidence

**Date** — 2026-11-08  
**Feature** — `001-fund-irr-wallet`  
**Environment** — .NET 10; Docker Desktop engine 27.5.1; pinned PostgreSQL 17.6 Testcontainers image. Synthetic gateway and customer data only.

## Final checks

| Check | Command or setup | Result |
| --- | --- | --- |
| Solution build | `dotnet build Avand.slnx --ignore-failed-sources --no-restore --disable-build-servers -m:1` | Pass, exit 0; 0 warnings and 0 errors. |
| Full V01 runtime suite | `dotnet test tests/Avand.V01.IntegrationTests/Avand.V01.IntegrationTests.csproj --logger 'trx;LogFileName=v01-final-postgres17.trx' --verbosity minimal` | Pass, exit 0; 15 passed, 0 failed, 0 skipped. The run built the two hosts and integration test assembly, then exercised both HTTP hosts against isolated PostgreSQL 17.6 databases and least-privilege roles. Result: `tests/Avand.V01.IntegrationTests/TestResults/v01-final-postgres17.trx`. |
| Definitive verify rejection | `dotnet test tests/Avand.V01.IntegrationTests/Avand.V01.IntegrationTests.csproj --filter FullyQualifiedName~DefinitiveVerifyRejectionFailsWithoutLedgerPosting` | Pass; explicit provider rejection leaves balance, journals and Ledger decisions unchanged. Also included in the full suite. |
| Inquiry edge cases | `dotnet test tests/Avand.V01.IntegrationTests/Avand.V01.IntegrationTests.csproj --filter FullyQualifiedName~GatewayInquiryTests` | Pass; 5 passed. Covers callback-free inquiry, amount and payment ID mismatch, definitive rejection, late expiry, issuer return, timeout and missing capture reference. Also included in the full suite. |
| Diff whitespace check | `git diff --check` | Pass; no whitespace errors. Git reported only the existing LF-to-CRLF normalization notices for the two project files. |

An earlier parallel fixture run hit Docker PostgreSQL readiness timeouts before some tests started. The suite is now configured to start fixture classes serially. The final run passed; concurrency and idempotency remain tested inside the test cases.

## AC coverage from the final runtime suite

| Acceptance criteria | Runtime evidence |
| --- | --- |
| AC01–AC04 | `TopUpAcceptanceTests`, `LedgerPostingTests`: exact IRR balance delta, single balanced journal, duplicate admission/callback behavior, customer isolation, eligibility rejection, scoped API and database role protections. |
| AC05–AC08 | `GatewayInquiryTests`: callback-free capture inquiry, late callback expiry, definitive verify rejection, timeout, mismatched amount/payment identity, missing capture reference, and confirmed issuer return without Avand journal or balance effects. |
| AC09–AC11 | `LedgerRecoveryTests`: verified payment remains pending while posting is delayed past the provider window, response loss followed by Application restart and same-identity recovery, and pre-commit failure with no partial journal, projection or decision. |
| AC12 | `RestrictedCreditTests`, `LedgerPostingTests`: restriction effective before first posting creates one restricted credit and blocks new admission; duplicate evidence does not post again; replay after restriction preserves the original available bucket. |
| AC13 | `GatewayBoundaryTests`: first verify just before, exactly at and just after the inclusive 15-minute boundary, plus inquiry for an already verified capture after the window. |

The static schema smoke check recorded during setup remains separate from runtime evidence. Final acceptance is supported by the two-host PostgreSQL-backed suite above.
