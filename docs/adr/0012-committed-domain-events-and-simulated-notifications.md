# ADR 0012: Committed-only domain events and simulated notifications

- Status: Accepted
- Date: 2026-10-29

## Context

The assignment requires simulated notifications for low stock, high-value money orders and every void or refund, using events or an observer. `LowStockNotificationRule` and `HighValueMoneyOrderNotificationRule` existed but were never called. A notification must never announce a change that was then rolled back.

## Decision

- **Events:** `TransactionCompletedEvent`, `StockLowEvent`, `MoneyOrderIssuedEvent`, `HighValueMoneyOrderIssuedEvent`, `TransactionVoidedEvent` and `TransactionRefundedEvent` (Domain).
- **Publishing after commit:** application services collect events inside the `UpdateAsync` callback and publish them through `DomainEventPublisher` only after `UpdateAsync` returns, that is, after the save.
- **Observer:** the publisher exposes one C# `event` per type. `SimulatedNotificationSender` subscribes at startup and writes the four required notifications as structured warning logs (`Simulated notification: …`). A throwing subscriber is reported through an error callback and does not affect the caller.
- **Low stock on crossing only** (decided by the author): a notification fires when a product goes from above `Reports:LowStockThreshold` to at or below it, not on every sale while it stays low. Without a configured threshold, no low-stock events are raised.

## Alternatives considered

- **Raise and dispatch inside the entity or the update callback.** It is simpler, but subscribers would run before the save and could notify about a sale that then failed.
- **MediatR or another messaging library.** Not in the allowed libraries, and in-process C# events are enough.
- **An outbox table for guaranteed delivery.** That is the right answer once there is a database; with JSON files and simulated delivery it adds complexity without a real channel.
- **Notify on every sale while stock is low.** It floods the channel with repeats.

## Consequences

Notifications always describe committed changes. Delivery is best-effort and in-process: if the process stops between the save and publishing, that notification is lost. Replacing the log sender with email, SMS or a queue means adding another subscriber.
