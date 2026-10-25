
namespace ApexPost.Web.Models;

public sealed record TransactionListViewModel(
	Guid Id,
	TransactionPaymentStatus PaymentStatus,
	decimal Total,
	string Currency,
	string? BranchCode);
