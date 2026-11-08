# Quickstart — V02 validation

## Prerequisites

Run from the repository root with the .NET 10 SDK and access to a Docker-compatible engine for the pinned PostgreSQL 17.6 image. The existing test fixture also supports `AVAND_V01_TEST_POSTGRES` for a disposable PostgreSQL test instance where the fixture account can create isolated databases and roles. Keep test credentials local and use synthetic identities, price observations and custody evidence. Check `dotnet --version` and `docker info` before running container-backed tests.

## Commands after implementation

```powershell
dotnet build Avand.slnx --ignore-failed-sources
dotnet test tests/Avand.V01.IntegrationTests/Avand.V01.IntegrationTests.csproj --no-build --filter "FullyQualifiedName~V02"
dotnet test tests/Avand.V01.IntegrationTests/Avand.V01.IntegrationTests.csproj --no-build --filter "FullyQualifiedName!~V02"
```

Use V02-named test classes so the focused filter selects the intended cases. Confirm discovered test counts. The second filter reruns the V01 suite on the evolved schema. A build or an empty test selection supplies no runtime acceptance evidence.

## Customer journey

1. Start both HTTP hosts with isolated test stores and roles, separate Trading and Payments credentials, controlled clocks and a valid paired price observation. Establish confirmed synthetic custody with a balanced opening entry. Fund a seeded eligible customer's spendable IRR by posting a V01 top-up.
2. Request a quote using the [customer contract](contracts/v02.md). Verify ask, weight, principal, fee, debit and expiry independently before confirming. Submit the quote ID with a fresh retry key.
3. Read order status and committed balances separately. At completion, inspect the exact IRR and mg deltas, separately balanced asset legs and the original Ledger result. Repeat the confirmation under its original key. Compare acknowledgements, durable results and counts.

## Acceptance matrix

| Spec cases | Exercise and observe |
| --- | --- |
| AC-01–04 | Compare distinct buy/sell prices, weight and principal floors and fee ceiling with an independent integer oracle. Exercise both configured limit boundaries, amount crossing below the floor after conversion and exact versus one-rial-short spendable funding. Check unspent IRR and no automatic resizing. |
| AC-05–06 | Read both committed balances and the balanced F04 journal after a real V01 top-up. Try anonymous, ineligible and foreign owners, a restriction effective before first decision and wrong service scope. Assert protected reads and no unauthorized effect. |
| AC-07–08 | Control source and both server clocks. Test age 30 seconds and just over it, malformed/missing/future timestamps and delayed delivery. Force first protected Ledger decision before, exactly at and after quote expiry. Verify a still-valid quote after feed failure and explicit confirmation of a replacement. |
| AC-09–12 | With independent connections and controlled overlap, isolate a same-wallet race and a shared-backing race with distinct quote and operation IDs. Keep other guards permissive. Check the loser reason and committed totals. Exercise normal/outage caps, nonzero outgoing commitments, at-cap and one-mg-over requests, unavailable evidence, advisory max and unconfirmed receipts. |
| AC-13 | Send 100 simultaneous identical authorized confirmations. Compare all acknowledgements. Verify one order, durable decision and journal and exact final deltas. Test key/payload conflict and consumed-quote reuse under a different key. |
| AC-14–15 | Prove the Ledger commit before dropping the reply. Advance beyond expiry and restart Application on the same stores. Verify recovered outcome and single effect. Separately delay the first decision past expiry, interrupt posting before commit, and interrupt Application result application after Ledger commit. Use fresh connections to inspect both stores. |
| AC-16 | Verify zero intent for a covered buy and for rejection, then one intent each for the initial and incremental deficit examples in the spec. Drop a response and repeat result application. Observe exact stored procurement quantities, no duplicate intent and no external execution. |
| Constitution and V01 regression | Migrate a populated V01 fixture and re-read original payment decisions, balances and wire responses. Try unauthorized financial DML under runtime credentials and invalid multi-asset legs/projection under controlled fixture authority. Verify rollback and existing V01 scenarios. |

Record actual commands, exit codes, observed outcomes and missing checks in `harness/evidence/002-buy-gold-wallet.md` using the [harness guide](../../docs/SDD_HARNESS.md). Record static arithmetic and accounting examples separately from PostgreSQL and two-service runtime results. V02 runtime checks are NotRun at the planning stage.
