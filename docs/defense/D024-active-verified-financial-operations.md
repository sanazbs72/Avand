# D024 - Require Active and Verified status for new customer financial instructions

**Status:** Confirmed policy; coordination mechanism proposed
**Design checkpoint:** 2026-11-06

[Decision log](../DECISIONS.md) | [Design](../DESIGN.md) | [Defense index](README.md)

## Decision

Only a Customer with Status = Active and VerificationStatus = Verified may initiate a new top-up, buy, sell or withdrawal. Check eligibility at acceptance; an older valid quote does not authorize a newly ineligible customer. Continue processing genuine results of previously initiated operations.

## Why this fits V1

The user explicitly approved one eligibility gate across the four financial operations. It is straightforward to explain and test while keeping financial recovery independent from new activity permission.

## Alternatives considered

- Authentication-only eligibility: does not express customer verification or suspension.
- Blocking all background processing after suspension: can hide successful earlier payments and leave financial records wrong.

## Tradeoffs and limits

Verification and lifecycle state require an authoritative owner. Concurrent status changes must be coordinated with local financial acceptance. This decision does not select a verification provider, claim regulatory sufficiency, or define exactly how funds received after suspension become restricted. Suspension does not delete assets or automatically reverse completed trades.

## Likely defense question

**If the customer is suspended before the gateway callback arrives, do you discard the callback?**

No. Verify and account for genuine success once because it is an earlier financial fact. Keep the customer barred from new instructions. A separate restriction policy determines what can be done with the received funds.

## Evidence to prepare

Exercise each financial initiation for Active/Verified, suspended, closed and unverified customers. Change eligibility after quote issuance and before confirmation. Race a restriction against acceptance under the selected concurrency contract. Process duplicate late payment success after suspension and confirm one financial posting.

This is planned verification. No implementation or test result is claimed.

## When to revisit

Refine the permission matrix and verification/closure transitions when defining onboarding, staff authorization, withdrawals and restricted-funds policy.

## Independent-service implications

Under D028, financial restriction becomes effective at Ledger acknowledgement/commit. Application blocks new local initiation when restriction is requested, and must show Pending until financial enforcement is acknowledged. Activation remains disabled until that acknowledgement. A prior genuine payment result is still accountable.
