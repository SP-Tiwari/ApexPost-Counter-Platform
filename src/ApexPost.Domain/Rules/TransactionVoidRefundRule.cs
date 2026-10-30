using ApexPost.Domain.Services;

namespace ApexPost.Domain.Rules;

public sealed class TransactionVoidRefundRule
{
	private static readonly TransactionAccessRule AccessRule = new();
	private static readonly SaleReversalService ReversalService = new();

	public void Void(
		Staff staff,
		TransactionBasket basket,
		string reason,
		IEnumerable<BranchStock> branchStocks)
	{
		ValidateAuthorization(staff, basket, reason);
		ReversalService.Void(basket, reason, branchStocks);
	}

	public void Refund(
		Staff staff,
		TransactionBasket basket,
		string reason,
		IEnumerable<BranchStock> branchStocks)
	{
		ValidateAuthorization(staff, basket, reason);
		ReversalService.Refund(basket, reason, branchStocks);
	}

	private static void ValidateAuthorization(
		Staff staff,
		TransactionBasket basket,
		string reason)
	{
		ArgumentNullException.ThrowIfNull(staff);
		ArgumentNullException.ThrowIfNull(basket);

		AccessRule.EnsureCanAccess(staff, basket);

		if (string.IsNullOrWhiteSpace(reason))
			throw new ArgumentException(
				"A reason is required to void or refund a transaction.",
				nameof(reason));
	}
}
