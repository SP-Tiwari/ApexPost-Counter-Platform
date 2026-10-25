using ApexPost.Domain.Services;

namespace ApexPost.Domain.Rules;

public sealed class TransactionProcessingRule
{
	public void Process(
		Staff staff,
		Branch branch,
		TransactionBasket basket,
		IEnumerable<BranchStock> branchStocks)
	{
		ArgumentNullException.ThrowIfNull(staff);
		ArgumentNullException.ThrowIfNull(branch);
		ArgumentNullException.ThrowIfNull(basket);
		ArgumentNullException.ThrowIfNull(branchStocks);

		if (!staff.IsActive)
			throw new InvalidOperationException("Inactive staff cannot process transactions.");

		if (!branch.IsActive)
			throw new InvalidOperationException("Transactions cannot be processed at an inactive branch.");

		if (staff.Role is not StaffRole.CounterClerk and not StaffRole.BranchManager)
			throw new InvalidOperationException(
				$"Staff role '{staff.Role}' is not authorized to process transactions.");

		if (staff.HomeBranchCode != branch.Code)
		{
			throw new InvalidOperationException(
				"Staff can process transactions only at their assigned branch.");
		}

		new SaleProcessingService().ProcessSale(
			basket,
			branch.Code,
			branchStocks);
	}
}
