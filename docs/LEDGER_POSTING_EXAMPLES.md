# Avand Ledger - Posting Examples

Date: 2026-11-06
Status: Numerical design examples, not executed application scenarios.

[Chart of accounts](CHART_OF_ACCOUNTS.md) | [Financial operations](FINANCIAL_OPERATIONS.md) | [JSON fixtures](examples/ledger-postings.json)

Prices and starting balances are illustrative. The main buy/sell examples use the company-confirmed 1% buy and 0.5% sell fees and satisfy the 1,000,000–5,000,000,000 IRR order-value range.

Each book starts independently. Baseline examples assume the contract's required authorization, holds and verified evidence; the static fixture checks validate journal direction, per-asset equality, nonnegative protected balances and stated final balances. They do not prove runtime authorization, locking, network recovery or hold behavior.

## baseline: Funded custody, top-up, buy, sell, payout, refund and treasury

Initial balances are zero.

### B01. Confirmed platform funding

Operation: ConfirmCapitalInflow. Record kind: Financial.

| Asset | Account | Owner | Debit | Credit |
| --- | --- | --- | ---: | ---: |
| IRR | BankCash | BankMain | 200,000,000 | 0 |
| IRR | PlatformCapital | Platform | 0 | 200,000,000 |



### B02. Confirmed procurement prepayment

Operation: RecordCustodianCashPayment. Record kind: Financial.

| Asset | Account | Owner | Debit | Credit |
| --- | --- | --- | ---: | ---: |
| IRR | CustodianAdvance | CustodianA | 100,000,000 | 0 |
| IRR | BankCash | BankMain | 0 | 100,000,000 |

A matching authorized platform cash reservation exists before dispatch.

### B03. Confirmed receipt of 10,000 mg

Operation: RecordCustodianGoldReceipt. Record kind: Financial.

| Asset | Account | Owner | Debit | Credit |
| --- | --- | --- | ---: | ---: |
| IRR | TradingRialClearing | Platform | 100,000,000 | 0 |
| IRR | CustodianAdvance | CustodianA | 0 | 100,000,000 |
| GOLD_MG | CustodianGold | CustodianA | 10,000 | 0 |
| GOLD_MG | PlatformGoldPosition | Platform | 0 | 10,000 |



### B04. Verified 20,000,000 IRR top-up

Operation: ApplyGatewayPaymentEvidence. Record kind: Financial.

| Asset | Account | Owner | Debit | Credit |
| --- | --- | --- | ---: | ---: |
| IRR | GatewayReceivable | GatewayA | 20,000,000 | 0 |
| IRR | CustomerRialAvailable | CustomerA | 0 | 20,000,000 |



### B05. Gateway cash received without fee

Operation: RecordGatewaySettlement. Record kind: Financial.

| Asset | Account | Owner | Debit | Credit |
| --- | --- | --- | ---: | ---: |
| IRR | BankCash | BankMain | 20,000,000 | 0 |
| IRR | GatewayReceivable | GatewayA | 0 | 20,000,000 |



### B06. Buy 1,000 mg for 10,000,000 IRR plus 100,000 fee

Operation: BuyGold. Record kind: Financial.

| Asset | Account | Owner | Debit | Credit |
| --- | --- | --- | ---: | ---: |
| IRR | CustomerRialAvailable | CustomerA | 10,100,000 | 0 |
| IRR | TradingRialClearing | Platform | 0 | 10,000,000 |
| IRR | FeeRevenue | Platform | 0 | 100,000 |
| GOLD_MG | PlatformGoldPosition | Platform | 1,000 | 0 |
| GOLD_MG | CustomerGoldAvailable | CustomerA | 0 | 1,000 |



### B07. Sell 500 mg for gross 4,900,000 IRR and 24,500 fee

Operation: SellGold. Record kind: Financial.

| Asset | Account | Owner | Debit | Credit |
| --- | --- | --- | ---: | ---: |
| IRR | TradingRialClearing | Platform | 4,900,000 | 0 |
| IRR | CustomerRialAvailable | CustomerA | 0 | 4,875,500 |
| IRR | FeeRevenue | Platform | 0 | 24,500 |
| GOLD_MG | CustomerGoldAvailable | CustomerA | 500 | 0 |
| GOLD_MG | PlatformGoldPosition | Platform | 0 | 500 |



### B08. Reserve a 5,000,000 IRR payout

Operation: ReserveWithdrawal. Record kind: Financial.

| Asset | Account | Owner | Debit | Credit |
| --- | --- | --- | ---: | ---: |
| IRR | CustomerRialAvailable | CustomerA | 5,000,000 | 0 |
| IRR | WithdrawalPayable | CustomerA | 0 | 5,000,000 |

Also creates a physical cash reservation, already represented by WithdrawalPayable.

### B09. Bank proves 2,000,000 IRR partial payment

Operation: SettleWithdrawal. Record kind: Financial.

| Asset | Account | Owner | Debit | Credit |
| --- | --- | --- | ---: | ---: |
| IRR | WithdrawalPayable | CustomerA | 2,000,000 | 0 |
| IRR | BankCash | BankMain | 0 | 2,000,000 |

The other 3,000,000 IRR remains reserved.

### B10. Bank proves the remaining 3,000,000 IRR

Operation: SettleWithdrawal. Record kind: Financial.

| Asset | Account | Owner | Debit | Credit |
| --- | --- | --- | ---: | ---: |
| IRR | WithdrawalPayable | CustomerA | 3,000,000 | 0 |
| IRR | BankCash | BankMain | 0 | 3,000,000 |



### B11. Reserve 1,000,000 IRR from the captured payment

Operation: ReserveRefund. Record kind: Financial.

| Asset | Account | Owner | Debit | Credit |
| --- | --- | --- | ---: | ---: |
| IRR | CustomerRialAvailable | CustomerA | 1,000,000 | 0 |
| IRR | RefundPayable | CustomerA | 0 | 1,000,000 |



### B12. Confirmed refund funded by bank debit

Operation: SettleRefund. Record kind: Financial.

| Asset | Account | Owner | Debit | Credit |
| --- | --- | --- | ---: | ---: |
| IRR | RefundPayable | CustomerA | 1,000,000 | 0 |
| IRR | BankCash | BankMain | 0 | 1,000,000 |



### B13. Operations sells 1,000 mg; cash not received

Operation: RecordCustodianGoldSale. Record kind: Financial.

| Asset | Account | Owner | Debit | Credit |
| --- | --- | --- | ---: | ---: |
| IRR | CustodianCashReceivable | CustodianA | 9,900,000 | 0 |
| IRR | TradingRialClearing | Platform | 0 | 9,900,000 |
| GOLD_MG | PlatformGoldPosition | Platform | 1,000 | 0 |
| GOLD_MG | CustodianGold | CustodianA | 0 | 1,000 |

An approved free-gold outflow hold exists. No usable bank cash is created yet.

### B14. Confirmed treasury proceeds

Operation: RecordCustodianCashReceipt. Record kind: Financial.

| Asset | Account | Owner | Debit | Credit |
| --- | --- | --- | ---: | ---: |
| IRR | BankCash | BankMain | 9,900,000 | 0 |
| IRR | CustodianCashReceivable | CustodianA | 0 | 9,900,000 |



### B15. Confirmed 10,000 IRR bank cost

Operation: RecordProviderFee. Record kind: Financial.

| Asset | Account | Owner | Debit | Credit |
| --- | --- | --- | ---: | ---: |
| IRR | ProviderFeeExpense | Platform | 10,000 | 0 |
| IRR | BankCash | BankMain | 0 | 10,000 |



### B16. Unidentified real cash receipt

Operation: RecordUnmatchedExternalMovement. Record kind: Financial.

| Asset | Account | Owner | Debit | Credit |
| --- | --- | --- | ---: | ---: |
| IRR | BankCash | BankMain | 250,000 | 0 |
| IRR | SuspenseCredit | CaseA | 0 | 250,000 |

Cash increases but equal suspense liability prevents free treasury headroom.

### B17. Receipt proven to be owner funding

Operation: ResolveSuspense. Record kind: Financial.

| Asset | Account | Owner | Debit | Credit |
| --- | --- | --- | ---: | ---: |
| IRR | SuspenseCredit | CaseA | 250,000 | 0 |
| IRR | PlatformCapital | Platform | 0 | 250,000 |



### B18. Existing mistaken customer credit, retained for correction

Operation: HistoricalMistake. Record kind: HistoricalMistake.

| Asset | Account | Owner | Debit | Credit |
| --- | --- | --- | ---: | ---: |
| IRR | SuspenseDebit | CaseB | 5,000 | 0 |
| IRR | CustomerRialAvailable | CustomerA | 0 | 5,000 |

This is deliberately wrong historical accounting, not a supported normal customer command.

### B19. Approved full reversal of the mistaken credit

Operation: PostCorrection. Record kind: Financial.

| Asset | Account | Owner | Debit | Credit |
| --- | --- | --- | ---: | ---: |
| IRR | CustomerRialAvailable | CustomerA | 5,000 | 0 |
| IRR | SuspenseDebit | CaseB | 0 | 5,000 |

Links to B18; the original entries stay immutable.

Expected final normal-side balances:

| Account instance | Balance |
| --- | ---: |
| IRR-1100 / BankMain | 124,140,000 |
| IRR-2100 / CustomerA | 8,775,500 |
| IRR-2130 / CustomerA | 0 |
| IRR-2140 / CustomerA | 0 |
| IRR-4100 / Platform | 124,500 |
| IRR-5100 / Platform | 10,000 |
| GOLD-1100 / CustodianA | 9,000 |
| GOLD-2100 / CustomerA | 500 |
| GOLD-3200 / Platform | 8,500 |

## capture-and-return-before-wallet: Captured money fully returned before wallet delivery

Initial balances are zero.

### R01. Record capture and already-confirmed gateway return atomically

Operation: ApplyGatewayPaymentEvidence. Record kind: Financial.

| Asset | Account | Owner | Debit | Credit |
| --- | --- | --- | ---: | ---: |
| IRR | GatewayReceivable | GatewayA | 1,000,000 | 0 |
| IRR | RefundPayable | CustomerB | 0 | 1,000,000 |
| IRR | RefundPayable | CustomerB | 1,000,000 | 0 |
| IRR | GatewayReceivable | GatewayA | 0 | 1,000,000 |

Both provider action references are claimed; no spendable customer credit appears.

Expected final normal-side balances:

| Account instance | Balance |
| --- | ---: |
| IRR-1110 / GatewayA | 0 |
| IRR-2140 / CustomerB | 0 |
| IRR-2100 / CustomerB | 0 |

## failed-payment-no-capture: Issuing bank returns an unverified card payment

Initial balances are zero.

### N01. No Ledger journal for money Avand never captured

Operation: ProviderFailureEvidenceOnly. Record kind: ProductOnly.

No Ledger journal.

Keep definitive verify rejection and gateway inquiry evidence in Application. The issuing bank handles the card return. Avand records no wallet credit, BankCash debit or refund dispatch.

Expected final normal-side balances:

No financial account changes.

## late-restricted-capture: Verified capture evidence delivered after restriction

Initial balances are zero.

### L01. Credit the real claim to Restricted

Operation: ApplyGatewayPaymentEvidence. Record kind: Financial.

| Asset | Account | Owner | Debit | Credit |
| --- | --- | --- | ---: | ---: |
| IRR | GatewayReceivable | GatewayA | 1,000,000 | 0 |
| IRR | CustomerRialRestricted | CustomerB | 0 | 1,000,000 |

Eligibility blocks new instructions, not earlier real payment evidence.

Expected final normal-side balances:

| Account instance | Balance |
| --- | ---: |
| IRR-2120 / CustomerB | 1,000,000 |
| IRR-2100 / CustomerB | 0 |
| IRR-1100 / BankMain | 0 |

## involuntary-reversal-after-spending: Record a proven reversal without negative customer balances

Initial normal-side balances:

| Account | Owner | Balance |
| --- | --- | ---: |
| BankCash | BankMain | 5,000,000 |
| GatewayReceivable | GatewayA | 2,000,000 |
| PlatformCapital | Platform | 5,000,000 |
| CustomerRialAvailable | CustomerA | 200,000 |
| TradingRialClearing | Platform | 1,800,000 |
| CustodianGold | CustodianA | 1,000 |
| PlatformGoldPosition | Platform | 820 |
| CustomerGoldAvailable | CustomerA | 180 |

### V01. Provider reverses the prior 2,000,000 IRR capture

Operation: ApplyGatewayPaymentEvidence. Record kind: Financial.

| Asset | Account | Owner | Debit | Credit |
| --- | --- | --- | ---: | ---: |
| IRR | CustomerRialAvailable | CustomerA | 200,000 | 0 |
| IRR | SuspenseDebit | ReversalCase | 1,800,000 | 0 |
| IRR | GatewayReceivable | GatewayA | 0 | 2,000,000 |

Create finance case and risk freeze; do not silently seize reserved assets or claim the suspense is usable money.

Expected final normal-side balances:

| Account instance | Balance |
| --- | ---: |
| IRR-2100 / CustomerA | 0 |
| IRR-1190 / ReversalCase | 1,800,000 |
| IRR-1110 / GatewayA | 0 |
| GOLD-2100 / CustomerA | 180 |

## gateway-paid-refund-before-merchant-debit: Provider refund and later merchant debit are distinct facts

Initial normal-side balances:

| Account | Owner | Balance |
| --- | --- | ---: |
| BankCash | BankMain | 5,000,000 |
| PlatformCapital | Platform | 3,000,000 |
| CustomerRialAvailable | CustomerA | 2,000,000 |

### M01. Reserve customer claim and bank funding

Operation: ReserveRefund. Record kind: Financial.

| Asset | Account | Owner | Debit | Credit |
| --- | --- | --- | ---: | ---: |
| IRR | CustomerRialAvailable | CustomerA | 1,000,000 | 0 |
| IRR | RefundPayable | CustomerA | 0 | 1,000,000 |



### M02. Provider paid customer; platform debit pending

Operation: SettleRefund. Record kind: Financial.

| Asset | Account | Owner | Debit | Credit |
| --- | --- | --- | ---: | ---: |
| IRR | RefundPayable | CustomerA | 1,000,000 | 0 |
| IRR | GatewayRefundPayable | GatewayA | 0 | 1,000,000 |

Cash reservation converts to a represented gateway payable; bank cash is unchanged.

### M03. Actual merchant cash debit

Operation: RecordGatewayRefundDebit. Record kind: Financial.

| Asset | Account | Owner | Debit | Credit |
| --- | --- | --- | ---: | ---: |
| IRR | GatewayRefundPayable | GatewayA | 1,000,000 | 0 |
| IRR | BankCash | BankMain | 0 | 1,000,000 |



Expected final normal-side balances:

| Account instance | Balance |
| --- | ---: |
| IRR-1100 / BankMain | 4,000,000 |
| IRR-2100 / CustomerA | 1,000,000 |
| IRR-2160 / GatewayA | 0 |

## custody-gold-before-payment: Confirmed gold and cash timing are independent

Initial normal-side balances:

| Account | Owner | Balance |
| --- | --- | ---: |
| BankCash | BankMain | 200,000,000 |
| PlatformCapital | Platform | 200,000,000 |

### P01. Receive authorized 10,000 mg before paying

Operation: RecordCustodianGoldReceipt. Record kind: Financial.

| Asset | Account | Owner | Debit | Credit |
| --- | --- | --- | ---: | ---: |
| IRR | TradingRialClearing | Platform | 100,000,000 | 0 |
| IRR | CustodianPurchasePayable | CustodianA | 0 | 100,000,000 |
| GOLD_MG | CustodianGold | CustodianA | 10,000 | 0 |
| GOLD_MG | PlatformGoldPosition | Platform | 0 | 10,000 |

Convert the matching cash commitment into the payable-backed reservation, not both deductions.

### P02. Pay the confirmed custody payable

Operation: RecordCustodianCashPayment. Record kind: Financial.

| Asset | Account | Owner | Debit | Credit |
| --- | --- | --- | ---: | ---: |
| IRR | CustodianPurchasePayable | CustodianA | 100,000,000 | 0 |
| IRR | BankCash | BankMain | 0 | 100,000,000 |



Expected final normal-side balances:

| Account instance | Balance |
| --- | ---: |
| IRR-1100 / BankMain | 100,000,000 |
| IRR-2150 / CustodianA | 0 |
| GOLD-1100 / CustodianA | 10,000 |

## unidentified-gold: Physical receipt is not automatically eligible backing

Initial balances are zero.

### G01. Record 50 mg whose ownership is unresolved

Operation: RecordUnmatchedExternalMovement. Record kind: Financial.

| Asset | Account | Owner | Debit | Credit |
| --- | --- | --- | ---: | ---: |
| GOLD_MG | CustodianGoldUnidentified | CustodianA | 50 | 0 |
| GOLD_MG | GoldSuspenseCredit | GoldCase | 0 | 50 |

Eligible C remains zero.

### G02. Ownership and grade proven

Operation: ResolveSuspense. Record kind: Financial.

| Asset | Account | Owner | Debit | Credit |
| --- | --- | --- | ---: | ---: |
| GOLD_MG | GoldSuspenseCredit | GoldCase | 50 | 0 |
| GOLD_MG | PlatformGoldPosition | Platform | 0 | 50 |
| GOLD_MG | CustodianGold | CustodianA | 50 | 0 |
| GOLD_MG | CustodianGoldUnidentified | CustodianA | 0 | 50 |

Eligible custody becomes 50 mg; the physical total does not increase.

Expected final normal-side balances:

| Account instance | Balance |
| --- | ---: |
| GOLD-1110 / CustodianA | 0 |
| GOLD-2190 / GoldCase | 0 |
| GOLD-1100 / CustodianA | 50 |
| GOLD-3200 / Platform | 50 |


## Arithmetic and capacity boundaries

- Buy budget 1,000,001 IRR at 9,999,999 IRR/g yields 100 mg and actual principal 1,000,000 IRR. At 1% fee, total debit is 1,010,000 IRR and 1 IRR of entered principal remains unused.
- A 1% fee on principal 1,000,001 IRR is 10,001 IRR after ceiling.
- Selling 100 mg at 9,999,999 IRR/g yields 999,999 IRR gross after floor and fails the proposed minimum.
- Confirmed custody 1,000 g, claims 995 g and no outgoing reservation gives 15 g capacity.
- With 5 g already reserved for physical outflow, effective custody is 995 g and capacity is 9.95 g under the proposed conservative reservation rule.

Baseline ending customer balances are 8,775,500 IRR and 500 mg. Confirmed bank cash is 124,140,000 IRR and eligible custody is 9,000 mg. Fee revenue is 124,500 IRR and the separately evidenced provider expense is 10,000 IRR.

A funded ledger can balance perfectly while a commercial policy is wrong. Use these numerical checks alongside the contract and the planned two-service/PostgreSQL acceptance tests.
