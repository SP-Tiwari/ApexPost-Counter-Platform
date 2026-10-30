
namespace ApexPost.Web.Models;

public sealed record TransactionDetailsViewModel(
	Guid Id,
	TransactionPaymentStatus PaymentStatus,
	decimal Subtotal,
	decimal Tax,
	decimal Total,
	string Currency,
	string? BranchCode,
	IReadOnlyList<TransactionLineViewModel> LineItems,
	IReadOnlyList<TransactionNotificationViewModel> Notifications,
	DateTimeOffset? OccurredAtUtc,
	string? CounterTerminalId,
	string? ClerkStaffId,
	string? PaymentMethod);

public sealed record TransactionLineViewModel(
	string Description,
	TransactionItemType Type,
	decimal UnitPrice,
	int Quantity,
	decimal Subtotal,
	string Currency,
	string? ShippingDetails);

public sealed record TransactionNotificationViewModel(
	string Type,
	string Reason);
