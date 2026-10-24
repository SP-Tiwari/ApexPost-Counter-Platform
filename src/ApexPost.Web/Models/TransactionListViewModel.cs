using ApexPost.Domain.Enums;

namespace ApexPost.Web.Models;

public sealed record TransactionListViewModel(
	Guid Id,
	TransactionPaymentStatus PaymentStatus,
	decimal Total,
	string Currency);
