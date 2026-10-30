using ApexPost.Application.Events;
using ApexPost.Domain.Events;

namespace ApexPost.Web.Notifications;

// Simulated notification channel: writes structured log entries instead of
// sending email or SMS. It subscribes to committed domain events only, so
// nothing is reported for an operation that failed. No sender, recipient,
// or identity-document details are logged.
public sealed class SimulatedNotificationSender(
	ILogger<SimulatedNotificationSender> logger)
{
	public void Subscribe(DomainEventPublisher publisher)
	{
		ArgumentNullException.ThrowIfNull(publisher);

		publisher.TransactionCompleted += OnTransactionCompleted;
		publisher.StockLow += OnStockLow;
		publisher.MoneyOrderIssued += OnMoneyOrderIssued;
		publisher.HighValueMoneyOrderIssued += OnHighValueMoneyOrderIssued;
		publisher.TransactionVoided += OnTransactionVoided;
		publisher.TransactionRefunded += OnTransactionRefunded;
	}

	private void OnTransactionCompleted(
		object? sender, TransactionCompletedEvent domainEvent) =>
		logger.LogInformation(
			"Transaction {TransactionId} completed at branch {BranchCode} by staff {StaffId} for {Total} {Currency}.",
			domainEvent.TransactionId,
			domainEvent.BranchCode.Value,
			domainEvent.StaffId,
			domainEvent.Total.Amount,
			domainEvent.Total.Currency);

	private void OnStockLow(object? sender, StockLowEvent domainEvent) =>
		logger.LogWarning(
			"Simulated notification: low stock for product {ProductSku} at branch {BranchCode}: {Quantity} remaining (threshold {Threshold}).",
			domainEvent.Notification.ProductSku.Value,
			domainEvent.Notification.BranchCode.Value,
			domainEvent.Notification.Quantity,
			domainEvent.Notification.Threshold);

	private void OnMoneyOrderIssued(
		object? sender, MoneyOrderIssuedEvent domainEvent) =>
		logger.LogInformation(
			"Money order {MoneyOrderId} issued in transaction {TransactionId} at branch {BranchCode} for {Amount} {Currency}.",
			domainEvent.MoneyOrderId,
			domainEvent.TransactionId,
			domainEvent.BranchCode.Value,
			domainEvent.Amount.Amount,
			domainEvent.Amount.Currency);

	private void OnHighValueMoneyOrderIssued(
		object? sender, HighValueMoneyOrderIssuedEvent domainEvent) =>
		logger.LogWarning(
			"Simulated notification: high-value money order {MoneyOrderId} in transaction {TransactionId} for {Amount} {Currency} (threshold {Threshold}).",
			domainEvent.MoneyOrderId,
			domainEvent.TransactionId,
			domainEvent.Notification.Amount.Amount,
			domainEvent.Notification.Amount.Currency,
			domainEvent.Notification.Threshold.Amount);

	private void OnTransactionVoided(
		object? sender, TransactionVoidedEvent domainEvent) =>
		logger.LogWarning(
			"Simulated notification: transaction {TransactionId} at branch {BranchCode} voided by staff {StaffId}. Reason: {Reason}",
			domainEvent.TransactionId,
			domainEvent.BranchCode?.Value,
			domainEvent.StaffId,
			domainEvent.Reason);

	private void OnTransactionRefunded(
		object? sender, TransactionRefundedEvent domainEvent) =>
		logger.LogWarning(
			"Simulated notification: transaction {TransactionId} at branch {BranchCode} refunded by staff {StaffId}. Reason: {Reason}",
			domainEvent.TransactionId,
			domainEvent.BranchCode?.Value,
			domainEvent.StaffId,
			domainEvent.Reason);
}
