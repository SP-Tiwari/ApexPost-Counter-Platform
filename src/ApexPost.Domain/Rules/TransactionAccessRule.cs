using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;

namespace ApexPost.Domain.Rules;

public sealed class TransactionAccessRule
{
	public bool CanAccess(Staff staff, TransactionBasket basket)
	{
		ArgumentNullException.ThrowIfNull(staff);
		ArgumentNullException.ThrowIfNull(basket);

		return staff.IsActive &&
			staff.Role == StaffRole.BranchManager &&
			basket.BranchCode is not null &&
			basket.BranchCode == staff.HomeBranchCode;
	}

	public void EnsureCanAccess(Staff staff, TransactionBasket basket)
	{
		ArgumentNullException.ThrowIfNull(staff);
		ArgumentNullException.ThrowIfNull(basket);

		if (!staff.IsActive)
			throw new InvalidOperationException(
				"Inactive staff cannot access transactions.");

		if (staff.Role != StaffRole.BranchManager)
			throw new InvalidOperationException(
				"An active Branch Manager is required to access transactions.");

		if (basket.BranchCode is null ||
			basket.BranchCode != staff.HomeBranchCode)
		{
			throw new InvalidOperationException(
				"Transactions are accessible only from the assigned branch.");
		}
	}
}
