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

		if (basket.SaleDetails is { } saleDetails)
		{
			if (!string.Equals(
				saleDetails.ClerkStaffId, staff.Id, StringComparison.Ordinal))
			{
				throw new InvalidOperationException(
					"The recorded clerk must be the staff member processing the sale.");
			}

			if (!branch.CounterTerminals.Any(terminal =>
				terminal.IsActive &&
				string.Equals(
					terminal.Id,
					saleDetails.CounterTerminalId,
					StringComparison.Ordinal)))
			{
				throw new InvalidOperationException(
					"Transactions can be processed only at an active counter terminal of the branch.");
			}
		}

		new SaleProcessingService().ProcessSale(
			basket,
			branch.Code,
			branchStocks);
	}
}
