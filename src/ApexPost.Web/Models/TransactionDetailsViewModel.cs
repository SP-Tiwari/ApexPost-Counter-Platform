using ApexPost.Domain.Enums;

namespace ApexPost.Web.Models;

public sealed record TransactionDetailsViewModel(
	Guid Id,
	TransactionPaymentStatus PaymentStatus,
	decimal Subtotal,
	decimal Tax,
	decimal Total,
	string Currency,
	IReadOnlyList<TransactionLineViewModel> LineItems,
	IReadOnlyList<TransactionNotificationViewModel> Notifications);

public sealed record TransactionLineViewModel(
	string Description,
	TransactionItemType Type,
	decimal UnitPrice,
	int Quantity,
	decimal Subtotal,
	string Currency);

public sealed record TransactionNotificationViewModel(
	string Type,
	string Reason);
