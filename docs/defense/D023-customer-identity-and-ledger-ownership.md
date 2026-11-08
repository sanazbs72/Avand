# D023 - Separate Customer, Identity and ledger ownership

**Status:** Confirmed separation; entity fields and identity mechanism proposed
**Design checkpoint:** 2026-11-06

[Decision log](../DECISIONS.md) | [Design](../DESIGN.md) | [Defense index](README.md)

## Decision

Customer is the business owner of financial accounts and operations. Identity authenticates a principal. Ledger records the customer's financial claims. Customer has no RialBalance or GoldBalance fields. Link orders, payments, ledger accounts and bank-account destinations through CustomerId. Resolve the acting CustomerId from the authenticated principal and its trusted mapping.

## Why this fits V1

This distinguishes a financial customer from a staff login and prevents profile updates from being a second financial write path. Customer/Identity remain inside Application API; Ledger is the one independent shared financial service.

## Alternatives considered

- One user object containing credentials, profile, balances and operations permissions: fewer initial types but mixed ownership and financial write paths.
- A separate customer microservice: an independent deployment and integration boundary are not needed for this team.

## Tradeoffs and limits

The proposed Customer fields are Id, IdentityUserId, Status, VerificationStatus and CreatedAtUtc. Exact identity storage, verification evidence and bank-account rules remain open. Staff identities need not have a Customer record. Ledger account labels UserRial and UserGold still refer to customer claims; they are not profile properties.

## Likely defense question

**Why is an authenticated User not automatically a Customer who can trade?**

Authentication establishes who is acting. A Customer record establishes the business owner and eligibility, while staff may act under separate, explicit permissions. The endpoint must derive customer ownership from a trusted identity mapping rather than accepting a supplied CustomerId as authority.

## Evidence to prepare

Show that two customers cannot access each other's accounts by changing a request ID. Demonstrate that profile changes do not change ledger balances. Audit staff actions with both actor identity and affected CustomerId.

This is planned verification. No implementation or test result is claimed.

## When to revisit

Refine the identity mapping, verification evidence and lifecycle when selecting the authentication and onboarding contracts. Keep financial account ownership explicit.

## Independent-service implications

CustomerId is a stable inter-service reference, not a cross-database foreign key. Application owns profile and authentication mapping. Ledger owns financial accounts and a versioned effective financial-eligibility gate; see D028.
