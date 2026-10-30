using ApexPost.Domain.Notifications;

namespace ApexPost.Domain.Events;

// Facts about state changes that have been saved. Application services raise
// them only after the change is persisted, so a failed operation raises none.
public abstract record DomainEvent(DateTimeOffset OccurredAtUtc);

public sealed record TransactionCompletedEvent(
	Guid TransactionId,
	BranchCode BranchCode,
	string StaffId,
	Money Total,
	DateTimeOffset OccurredAtUtc) : DomainEvent(OccurredAtUtc);

public sealed record StockLowEvent(
	LowStockNotification Notification,
	DateTimeOffset OccurredAtUtc) : DomainEvent(OccurredAtUtc);

public sealed record MoneyOrderIssuedEvent(
	Guid MoneyOrderId,
	Guid TransactionId,
	BranchCode BranchCode,
	Money Amount,
	DateTimeOffset OccurredAtUtc) : DomainEvent(OccurredAtUtc);

public sealed record HighValueMoneyOrderIssuedEvent(
	Guid MoneyOrderId,
	Guid TransactionId,
	HighValueMoneyOrderNotification Notification,
	DateTimeOffset OccurredAtUtc) : DomainEvent(OccurredAtUtc);

public sealed record TransactionVoidedEvent(
	Guid TransactionId,
	BranchCode? BranchCode,
	string StaffId,
	string Reason,
	DateTimeOffset OccurredAtUtc) : DomainEvent(OccurredAtUtc);

public sealed record TransactionRefundedEvent(
	Guid TransactionId,
	BranchCode? BranchCode,
	string StaffId,
	string Reason,
	DateTimeOffset OccurredAtUtc) : DomainEvent(OccurredAtUtc);
