# Avand Gold V1 Company Clarifications

Source: The company's answers to questions 1–17, supplied by the user on 2026-11-07. These answers govern the corresponding V1 product behavior where they conflict with earlier design assumptions.

| Question | Confirmed requirement |
| --- | --- |
| Q1 | Purchases use the existing IRR wallet. The payment gateway is for wallet top-up only. |
| Q2 | The minimum value of a buy or sell order is 1,000,000 IRR. |
| Q3 | The smallest quantity is 1 mg. Round quantity down. Unused buy principal stays in the wallet. |
| Q4 | The maximum value per order is 5,000,000,000 IRR. Keep order limits configurable for later changes. |
| Q5 | The price service sends buy and sell prices together. The spread is separate from transaction fees. |
| Q6 | An expired quote cannot execute. Show a new quote and require customer confirmation again. |
| Q7 | Quote validity is 30 seconds from server issuance. The client clock is advisory. |
| Q8 | Configurable rates start at 1% for buys and 0.5% for sells. |
| Q9 | Calculate the fee on the IRR order value. |
| Q10 | V1 has no separate minimum or maximum transaction fee. |
| Q11 | Receive callbacks regardless of arrival time and rely on server-to-server verification. The mock gateway rejects verification after its 15-minute window. Record that definitive rejection as a failed top-up. |
| Q12 | For an unverified payment, the issuing bank handles the return to the card within 72 hours under the supplied scenario. Avand inquires with the gateway, records the verified final status and shows the corresponding message. |
| Q13 | Once a sale is final, its net IRR proceeds are immediately available for purchases and withdrawals. |
| Q14 | The minimum withdrawal is 1,000,000 IRR. The ordinary per-request ceiling is 2,000,000,000 IRR. Larger requests require manual Finance approval and payment. Verify that the Sheba belongs to the customer using their national ID. |
| Q15 | Gold at the custodian is pooled. Individual customer claims exist in Avand's Ledger. |
| Q16 | The custodian sends a daily CSV with total gold and the day's inflows and outflows. Avand defines the mock format. |
| Q17 | IRR settlement with the custodian is daily on business days. |

## Interpretation in the design

- Validate both order limits inclusively against the final pre-fee IRR principal for buys and gross IRR value for sells. The numeric limits and fee base come from the company. The precise pre-fee, post-conversion boundary is the design interpretation. Wallet funding still includes the buy fee, and sell liquidity covers the net wallet credit.
- Quote expiry, gateway verify expiry and Ledger delivery are distinct. Late delivery of already verified capture evidence can still post once. A late callback does not make an expired, previously unverified capture successful. A transport timeout leaves the outcome unresolved until inquiry supplies a definitive result.
- Route above-ceiling withdrawals to Finance with no automatic dispatch. The approval shape, funding and hold timing for manual payment remain detailed design choices. The wallet debit and bank settlement still require the controlled Ledger path.
- The CSV reports physical custody evidence. It does not prove bank settlement. [Design section 13.1](DESIGN.md#131-proposed-mock-custody-csv) proposes a mock CSV format. Calendar, cutoffs and reconciliation tolerances belong in the applicable feature spec. The company answers do not select a healthy-mode uncovered-gold limit or a feed-freshness threshold.
- Existing post-capture refund, reversal and correction templates describe separate verified financial events. The issuer return of an unverified card payment creates no Avand refund workflow or wallet journal.
