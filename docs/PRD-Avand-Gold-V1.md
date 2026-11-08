# Avand Gold V1 PRD

**Status:** Product draft with proposed delivery slices. Confirmed behavior comes from the [company answers](COMPANY_CLARIFICATIONS.md) and the [decision log](DECISIONS.md). Resolve each slice's open questions in its active Spec Kit specification.

## Product and scope

Avand Gold V1 lets eligible customers fund an IRR wallet, trade fractional gold at a confirmed price, and withdraw available IRR to their own bank account. Avand tracks individual gold claims in Ledger while the custodian holds pooled gold. Finance and Operations need reliable evidence for payouts, custody, liquidity and discrepancies.

The customer segment is an inferred product audience from the brief: customers who want to buy or sell small amounts of gold and see the exact price, fee and available funds before they act. The value proposition is a clear quote, a traceable outcome and immediately usable net proceeds after a sale. Finance and Operations are internal users with a separate job: resolve exceptions using verified evidence and controlled financial actions. The acceptance outcomes below measure V1 delivery. Adoption and conversion targets require separate product evidence.

Gold and its required support flows are in V1. Fund and Card are design-only. Application owns workflow and Ledger owns financial effects. Each slice delivers a usable customer or operator outcome across whichever entry point, service and persistence boundaries it needs. The first slice introduces only the shared capabilities it exercises. Each slice can be demonstrated with controlled provider fixtures and is specified, clarified, planned, tasked and analyzed independently before implementation.

## Proposed vertical delivery slices

### V01. Fund the IRR wallet and see the result

**Outcome.** An eligible customer starts a mock gateway top-up and can check its result and IRR balance. This first slice runs from the Application entry point through gateway verification to one atomic Ledger credit and an owned balance/status read. It introduces the minimum Ledger posting and replay path.

**Acceptance.** A verified capture credits IRR once across duplicate callbacks and inquiry. Missing callbacks can be resolved by inquiry. A definitive rejection after the mock verify window creates no wallet credit or Avand refund dispatch. A previously verified capture recovers once after a lost Ledger response. An interrupted posting leaves no partial financial effect. Top-up does not trigger a gold purchase.

**Clarify.** Mock verify-window start and exact boundary, gateway evidence and status mapping, and the bucket for verified funds received after restriction. Use a seeded authenticated, eligible customer for the first demonstration under the approved demo contract.

### V02. Buy gold from the wallet at a confirmed quote

**Depends on** V01's wallet and posting path. **Outcome.** The customer sees the buy price, fee, gold quantity and total IRR debit, confirms once and sees the resulting order and balances. A posted purchase creates any required procurement intent once. Controlled confirmed custody can fund the first demonstration.

**Acceptance.** Use the buy side of the paired price and the configured buy fee. Enforce order bounds, whole-mg rounding, funding including fees and backing at confirmation. Expiry requires a fresh quote and confirmation. The same-key request burst yields one order and one Ledger effect. Distinct valid purchases competing for one wallet or shared backing cannot overspend it. Lost response recovers the original result and follow-up intent.

**Clarify.** Order-value basis at the limits, exact principal rounding, feed freshness and the applicable normal-mode backing policy. The earlier approved outage allowance is a separate policy. Confirm safe handling when capacity or evidence is insufficient.

### V03. Sell gold for immediately usable IRR

**Depends on** V02's quote and financial decision path. **Outcome.** The customer sees the sell price, fee and net proceeds, then sees those proceeds in the spendable and withdrawable IRR wallet after posting.

**Acceptance.** Use the sell side of the paired price and the configured sell fee. Enforce order bounds, expiry, owned gold and confirmed liquidity in the same Ledger decision. Concurrent sells cannot exceed available gold or shared liquidity. A receivable cannot fund a sale and an unfunded sale leaves no posting. Verify immediate wallet availability here and the payout journey in V04.

**Clarify.** Sell amount and rounding basis, and the approved definition of confirmed liquidity and obligations for admission. Controlled verified cash funding is a demonstration prerequisite.

### V04. Withdraw available IRR to a verified Sheba

**Depends on** V01 for available IRR and V03 for the sale-to-withdrawal scenario. **Outcome.** The customer requests an ordinary withdrawal and can see reservation, processing and final payment status.

**Acceptance.** Enforce the minimum and ordinary ceiling. Check Sheba ownership against the customer's national ID before payment. Reserve once, dispatch with a stable external identity and settle only against verified evidence. An ambiguous bank outcome retains protection. Definitive safe cancellation releases only the unused amount. Show above-ceiling requests as requiring the Finance route in V05.

**Clarify.** Mock ownership evidence, bank result mapping, approved reservation timing and fee handling. Include partial settlement if the approved V1 bank contract supports it.

### V05. Complete an above-ceiling withdrawal through Finance

**Depends on** V04. **Outcome.** Finance reviews a larger request and can record an approved manual payment. The customer can see the review and payment status.

**Acceptance.** A request above the ordinary ceiling enters Finance review without automatic dispatch or splitting. Approval binds to the request and verified destination. Funds and cash are checked through Ledger, payment evidence settles once, and the same amount cannot be paid again through the ordinary route.

**Clarify.** Approval roles and record, funding and hold timing, and evidence that establishes manual payment finality.

### V06. Replenish pooled custody after posted buys

**Depends on** V02's durable procurement intent. **Outcome.** Operations can trace a posted customer buy to the resulting pooled procurement work where needed, through confirmed physical receipt and separately evidenced cash settlement.

**Acceptance.** A replayed buy result cannot duplicate procurement intent. Unknown external execution remains visible and follows the original provider identity. Pending procurement does not increase backing. Verified receipt updates eligible physical custody once. Cash settlement follows the business-day contract and uses separate evidence. A procurement delay preserves the posted customer trade.

**Clarify.** Mock procurement execution and grouping, receipt and cash evidence, cutoff calendar, and behavior on stale custody evidence. V02's seeded-custody demonstration is distinct from enabling the live procurement path.

### V07. Restore liquidity through a manual treasury request

**Depends on** V03's liquidity guard. **Outcome.** Operations can see low liquidity, track a request, arrange funding or an authorized sale of free platform gold, and record the verified proceeds.

**Acceptance.** Low liquidity produces actionable work. A TreasuryRequest or uncollected sale proceeds do not increase usable cash. Confirmed inflow updates liquidity once. Any treasury gold sale protects customer backing and requires manual Operations action. A subsequent eligible customer sale can proceed after funded capacity is restored.

**Clarify.** Alert threshold, responsible role, authorization and allowed funding routes for the first implementation.

### V08. Reconcile statements and resolve an exception

**Depends on** the flows included in the reconciliation window. Start with one provider and add the other sources as those slices land. **Outcome.** An authorized operator can compare provider evidence with Application and Ledger records, assign a discrepancy and see the evidence needed to close it.

**Acceptance.** Find external-only and internal-only discrepancies and a Ledger `Posted` result still unresolved in Application. Replay the same evidence without another financial effect. Reconcile daily custody CSV separately from business-day IRR settlement. Unknown ownership stays in suspense until evidence and authority support resolution. Case closure links to the original outcome or an authorized corrective posting where required.

**Clarify.** Statement coverage and cutoffs, mock CSV, match and tolerance rules, aging, ownership and closure policy. Use the [reconciliation design](DESIGN.md#13-reconciliation) for details.

## Start the first Spec Kit feature

Use **V01** as the first feature input. Carry its outcome, acceptance criteria, open decisions and applicable sources into `spec.md`. Use the [Harness](SDD_HARNESS.md) to run Specify → Clarify → Plan → Tasks → Analyze → Implement. Put implementation tasks and evidence status in that feature's artifacts. The V01–V08 IDs provide product traceability across features.

Use the [acceptance mapping](DESIGN.md#14-scenario-traceability) to retain the original brief's scenarios alongside their Gold V1 adaptations. Use [Financial Operations](FINANCIAL_OPERATIONS.md) and the [Ledger Service design](LEDGER_SERVICE.md) for detailed contracts. The criteria in this PRD are planned outcomes awaiting execution evidence.
