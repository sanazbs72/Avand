# D036 - Apply company-confirmed V1 product rules

**Status:** Company-confirmed requirements. The design interpretations and open integration choices are listed separately.

[Company clarifications](../COMPANY_CLARIFICATIONS.md) | [Decision log](../DECISIONS.md) | [Defense index](README.md)

## Decision and rationale

Use Q1–Q17 as the product baseline. Configurable order limits and distinct fee rates define the economics captured in each quote. The server decides expiry, so an expired order needs a new quote and confirmation. Confirmed wallet-only purchase and pooled custody preserve the chosen Ledger and service boundaries.

Treat callbacks as notifications and verify with the gateway. The mock's 15-minute rule governs verification, while the issuing bank owns the unverified card return. Durable Ledger delivery recovers a verified capture separately. Larger withdrawals need a manual Finance path and customer-owned Sheba evidence. Daily custody CSV and business-day cash settlement are distinct reconciliation inputs.

## Tradeoffs and evidence

- Show quote price, spread and fee separately. Check both configurable order limits, rounding and fee-inclusive wallet funding at their boundaries.
- Test successful verify within the window, definitive mock expiry, late notification for already verified capture, missing callback with inquiry, and no wallet credit or Avand refund for an unverified payment.
- Test national-ID/Sheba mismatch, at-limit withdrawal and above-limit Finance routing. Verify the manual route cannot dispatch without the required approval and financial authorization.
- Reconcile duplicate or mismatched daily custody reports and separately verified cash settlement. Keep the business calendar and CSV schema explicit in the mock.

These are planned checks. Exact provider contracts and operational policy are still open where the company did not specify them.
