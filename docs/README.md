# Avand V1 Design Documents

Date: 2026-11-06
Status: Working design checkpoint.

These English documents capture the decisions made in the design discussion. They are not a claim that the complete take-home assignment has been designed or implemented.

## Read in this order

Start with [Company clarifications](COMPANY_CLARIFICATIONS.md) for the current Q1–Q17 product rules and their effect on earlier assumptions.

1. [Design](DESIGN.md): behavior, boundaries, financial examples, flows, scenario mapping and open work.
2. [Decision log](DECISIONS.md): what was decided, what is proposed, and what changed from earlier assumptions.
3. [Defense guide](defense/README.md): a separate defense file for every decision, with rationale, alternatives, tradeoffs, likely questions and planned evidence.
4. [Project structure](PROJECT_STRUCTURE.md): the selected two-host layout and database ownership.
5. [Ledger service](LEDGER_SERVICE.md): service boundary, durable recovery and time/team scope.
6. [Chart of accounts](CHART_OF_ACCOUNTS.md): the complete draft baseline account catalog and capacity formulas.
7. [Financial operations](FINANCIAL_OPERATIONS.md): typed financial/control contracts, exact math, posting and failure behavior.
8. [Posting examples](LEDGER_POSTING_EXAMPLES.md): numerical books with machine-readable fixtures.

## How to use the defense material

Use it to explain the choices in your own words. It contains no invented employment history, production incidents or benchmark results. Add your real integration experience before the defense.

Every defense file states its decision status. A confirmed business policy is not the same as an approved implementation detail, completed code, regulatory approval or a passing test.

## Current boundaries

D036 records the company-confirmed order limits, paired pricing, fees, gateway behavior, withdrawal routing and custody cadence. D030-D033 specify the detailed draft account and operation contract. Draft calculations and integration choices remain labeled where they require review.

D026-D029 document the independent service choice, atomic-operation recovery, financial authority and resource-aware delivery scope. Application order state and Ledger posting do not share a transaction. No Saga framework is required for the selected V1 slice.

The selected stack is C# on .NET 10 with PostgreSQL. The user selected an independently deployed Ledger and a modular Application API. D019 and D025 are superseded historical alternatives. EF Core/Npgsql, exact API shapes and implementation details remain working design choices.

The callback scenarios deliberately follow independent wallet top-up. Their divergence from the brief is explicit in D020.

Customer identity and financial eligibility are recorded in D023-D024. Only Active, Verified customers may initiate new financial operations; accounting for previously initiated activity continues after a restriction. D028 defines a working effective-time protocol across the service boundary.

The design still needs the open items in DESIGN section 16 before it is implementation-ready or submission-ready. No implementation plan, application code, migrations, commits, tags or executable tests were created as part of this documentation checkpoint.

## Future products: design only

- [Fund flow](FUND_PRODUCT_FLOW.md): subscription, allocation, redemption and delayed cash.
- [Card flow](CARD_PRODUCT_FLOW.md): funded authorization, liquidation, capture, release and settlement.

These documents add no V1 implementation scope. Partner activation conditions and new policy proposals are explicit.
