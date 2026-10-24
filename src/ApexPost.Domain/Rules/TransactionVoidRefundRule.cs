using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;

namespace ApexPost.Domain.Rules;

public sealed class TransactionVoidRefundRule
{
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

		if (!staff.IsActive || staff.Role != StaffRole.BranchManager)
			throw new InvalidOperationException(
				"An active Branch Manager is required to void or refund a transaction.");

		if (string.IsNullOrWhiteSpace(reason))
			throw new ArgumentException(
				"A reason is required to void or refund a transaction.",
				nameof(reason));
	}
}
