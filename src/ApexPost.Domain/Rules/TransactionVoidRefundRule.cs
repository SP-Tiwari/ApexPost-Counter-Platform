
namespace ApexPost.Domain.Rules;

public sealed class TransactionVoidRefundRule
{
	private static readonly TransactionAccessRule AccessRule = new();

	public void Void(Staff staff, TransactionBasket basket, string reason)
	{
		ValidateAuthorization(staff, basket, reason);
		basket.Void(reason);
	}

	public void Refund(Staff staff, TransactionBasket basket, string reason)
	{
		ValidateAuthorization(staff, basket, reason);
		basket.Refund(reason);
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
