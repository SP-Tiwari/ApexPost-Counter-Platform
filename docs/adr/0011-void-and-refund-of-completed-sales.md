# ADR 0011: Void and refund of completed sales

- Status: Accepted
- Date: 2026-10-29

## Context

Sales are saved as Paid the moment they are processed. Void accepted only Open baskets, so a completed sale could never be voided (409), and a refund changed the status without returning stock. The assignment requires voids and refunds with the Branch Manager role and a reason.

## Decision

- **Transitions:** Open → Voided, Paid → Voided and Paid → Refunded. Other transitions are rejected.
- **Who:** an active Branch Manager of the transaction's branch, with a non-empty reason (`TransactionVoidRefundRule`).
- **Reversal:** `SaleReversalService` validates first, then changes the status, then:
  - returns each goods line's quantity to branch stock, for paid sales only, since open baskets never deducted;
  - marks the sale's money orders `Cancelled`.

  It all happens inside the same atomic repository update, so a reversal happens at most once.
- **Policy choices confirmed with the author:**
  - no time limit on voids or refunds;
  - voids and refunds are allowed at a closed branch;
  - demo seed sales deduct stock, so reversing them is consistent.

## Alternatives considered

- **Refunds return money only, with stock handled by a separate stock adjustment.** That relies on staff remembering, and the trainer explicitly flagged missing stock return.
- **Void only Open baskets and refund Paid ones.** Most sales go straight to Paid, so void would stay unusable.
- **A time limit or "same business day" rule for voids.** The assignment does not specify one, so none was invented.

## Consequences

Completed sales can be voided or refunded end to end, with stock and money orders restored exactly once. Reversal emits `TransactionVoidedEvent` or `TransactionRefundedEvent` (ADR 0012). The status and reason are persisted with the basket.
