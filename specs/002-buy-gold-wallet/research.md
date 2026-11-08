# Research — V02 wallet-funded gold purchase

## R1. Feature authority and exact quote arithmetic

**Decision** — Apply the five answers in the [V02 spec](spec.md) over the proposed D032 defaults for this slice. Use integer IRR, whole milligrams and a positive integer ask in IRR per gram for the synthetic provider. Represent configured buy rate as whole parts per million, initially 10,000. With requested IRR B, ask A and rate R, compute q = floor(1000 × B / A), P = floor(q × A / 1000), F = ceil(P × R / 1,000,000), D = P + F. Use wide integer intermediates and checked conversion to stored units. Use one pure calculation source compiled into both hosts and an independent test oracle. The Ledger verifies P, F and D from the attested quote weight and quote policy. Application binds B and q at issuance. Capture the quoted rate/version, keep its policy definition available for validation, and enforce currently effective order bounds and financial controls on first decision. A new policy can reject a purchase but cannot alter the displayed amounts.

**Rationale** — Both rounding directions, the final pre-fee limit basis and exact wallet debit affect admission. Storing an approved quote version preserves the promised fee. Business outcomes depend on current funds and capacity. The [draft F04](../../docs/FINANCIAL_OPERATIONS.md#f04-buygold) remains the contract baseline with the V02 overrides in the [feature contract](contracts/v02.md).

**Alternatives considered** — Adopt the shared draft principal ceiling, use approximate decimal amounts, or duplicate conversion logic in endpoints. Each would diverge from the approved rule or create multiple implementations of the same arithmetic.

## R2. Quote source and time boundaries

**Decision** — Use an injectable paired-price reader in Application with a configured synthetic adapter for development and a deterministic fixture for tests. A positive buy/sell pair carries the provider observation time. At issuance, require provider age in the inclusive interval 0–30 seconds. Use injected server clocks for issuance and the protected first Ledger decision. Quote lifetime is the half-open interval from issue time to issue time plus 30 seconds. Unavailable provider, invalid observation or missing valid policy blocks new issuance. Confirmation uses the stored quote even when the feed later fails. A committed result bypasses first-decision time checks on replay.

**Rationale** — This distinguishes market-data freshness, customer authorization and financial finality. The repository has no approved live price-provider contract. V02 can verify the full interaction with controlled observations.

**Alternatives considered** — Use receive time or query the feed again on confirmation. Receive time accepts delayed prices and repricing invalidates the customer's approved economics.

## R3. Admission, transport and forward recovery

**Decision** — Use existing two hosts and databases. Keep V01 Payment routes and response shapes. Share the internal Ledger route mapping across typed Payment and Trading handlers with scoped service credentials. Use Application-local buy quote, order and leased recovery work. V01's recovery record has a mandatory top-up relationship and its worker resolves one payment handler. Reuse its lease, version and retry pattern for buy-specific work. Atomically persist the order, idempotency and quote claims, immutable request, permanent Ledger identity and work. Persist the financial decision and any procurement intent together when applying the Ledger result to the order. Stable customer acknowledgement and evolving order status are separate.

**Rationale** — Local transactions and stable identities cover admission races and both response loss and Application restart. Payments and Trading need distinct authorization and typed business claims even when they use the same Ledger wallet.

**Alternatives considered** — Reuse the payment-shaped table directly, dispatch before persistence, or write Application and Ledger state together. These respectively introduce fake top-up links, lose recovery identity or cross ownership boundaries.

## R4. Ledger guards and storage

**Decision** — Add a protected shared backing resource for the configured V02 gold pool and serialize all mutations of its eligible custody, active outflow commitments and customer claims. Within a short Ledger transaction, resolve replay under the authenticated operation identity, then protect backing, effective eligibility and account rows in a stable order before sampling the decision time. Reuse the V01 wallet row and lock order for any operation sharing those accounts. The Ledger decision captures incremental deficit for procurement at the same protected state used for acceptance.

**Rationale** — Producer-local advisory locking cannot protect capacity across writers. An Application precheck or separate inventory read cannot authorize a buy. The same state must authorize the decision, supply the incremental deficit and move the balances.

**Alternatives considered** — A single Application mutex, producer-only lock or optimistic read before an independent posting transaction. None protects the shared financial resource across service instances and writers.

## R5. Multi-asset posting and migration

**Decision** — Evolve the existing journal and projection, preserving historical F02 results and IRR accounts. Add explicit asset, normal side and journal template identity. Backfill V01 as IRR and update the deferred checks to validate F02 and F04 separately, balance each asset and verify signed projections. Add a narrowly scoped Ledger-owned posting boundary for both templates with runtime execute permission and no direct journal or projection DML. Keep financial decisions and posting within the same local transaction. Reconcile protected account locking with the reduced privileges using the Ledger-owned boundary. Test the owner/runtime grants and a migration over populated V01 data.

**Rationale** — V01's migration assumes two IRR lines, unsigned projections and nonnegative balances on every account. F04 has two assets and a signed platform position, while customer spendable balances must stay nonnegative. Direct journal append could bypass business deduplication even if its legs balance.

**Alternatives considered** — Keep a separate V02 journal or let the runtime continue arbitrary financial DML with only deferred balancing. Separate journals split the shared wallet history, and balancing alone cannot authorize a write.

## R6. Verification and provider boundaries

**Decision** — Extend the V01 xUnit integration project with named V02 classes. Reuse the two real HTTP hosts and PostgreSQL 17.6 fixture infrastructure with separate runtime roles. Fund an acceptance wallet through V01 capture, and establish starting synthetic custody with balanced, privileged fixture setup. Control both clocks, source observations and custody evidence. Verify real contention with independent connections and deterministic barriers or a held guard. Prove commit before suppressing a response and test rollback separately. See [quickstart](quickstart.md).

**Rationale** — Matching acknowledgements or a static posting example cannot prove single posting, PostgreSQL races or two-service recovery. V02 owns durable procurement intent and V06 owns aggregation and external execution. Synthetic custody inputs exercise admission without requiring a live custodian.

**Alternatives considered** — Mock the Ledger, use a shared connection, or integrate a live feed and custodian in V02. The first two hide the failure modes under test and the third introduces contracts outside this slice.
