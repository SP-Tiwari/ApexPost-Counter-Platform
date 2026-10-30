# ADR 0014: Transaction dates and sale details

- Status: Accepted
- Date: 2026-10-29

## Context

Baskets stored no date, so the daily sales report showed all-time totals every day. The assignment also requires each transaction to record its counter, clerk and payment method, and the basket allowed only one product line.

## Decision

- **Dates:** each basket gets `OccurredAtUtc`, set from the injected `TimeProvider` when it is created (schema 5). The daily report filters by business date on that timestamp in UTC (`TransactionOnBusinessDateSpecification`). Legacy undated sales stay undated and are left out of dated reports.
- **Sale details:** `CounterSaleDetails` (counter terminal ID, clerk staff ID, payment method) is stored per basket (schema 7).
  - The clerk must be the processing staff member.
  - The counter must be an active terminal of the branch; inactive counters are rejected (decided by the author).
  - The payment method must be one of `Transactions:PaymentMethods`, a configured list with no defaults (decided by the author).
- **Multiple lines:** the basket accepts many goods lines plus shipping and money-order lines in one sale.

## Alternatives considered

- **Branch-local business dates with time zones.** More correct for real branches, but no time zone is defined for branches, so UTC was used rather than inventing one.
- **Dates on report entries only.** Reports would be dated while the transactions themselves still had no history.
- **A payment-method enum, or free text.** An enum hard-codes business policy; free text cannot be reported on. A configured list does neither.

## Consequences

Daily reports reflect real dates. A UTC day may differ from a branch's local trading day, and this needs revisiting if branches get time zones. Counter sales stay disabled until payment methods and tax rates are configured.
