
namespace ApexPost.Domain.Rules;

public sealed class LowStockNotificationRule
{
	public LowStockNotification? Evaluate(BranchStock branchStock, int threshold)
	{
		ArgumentNullException.ThrowIfNull(branchStock);

		if (threshold < 0)
			throw new ArgumentOutOfRangeException(
				nameof(threshold), "Low-stock threshold cannot be negative.");

		return branchStock.Quantity <= threshold
			? new LowStockNotification(
				branchStock.BranchCode,
				branchStock.ProductSku,
				branchStock.Quantity,
				threshold)
			: null;
	}
}
