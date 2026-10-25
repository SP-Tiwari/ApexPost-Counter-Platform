
namespace ApexPost.Domain.Reporting;

public sealed record LowStockReportRow(
	BranchCode BranchCode,
	Sku ProductSku,
	int Quantity,
	int Threshold);

public sealed class LowStockReportService
{
	public IReadOnlyList<LowStockReportRow> CreateReport(
		IEnumerable<BranchStock> branchStocks,
		int threshold)
	{
		ArgumentNullException.ThrowIfNull(branchStocks);

		if (threshold < 0)
			throw new ArgumentOutOfRangeException(
				nameof(threshold), "Low-stock threshold cannot be negative.");

		var rows = new List<LowStockReportRow>();
		foreach (var stock in branchStocks)
		{
			ArgumentNullException.ThrowIfNull(stock);

			if (stock.Quantity <= threshold)
			{
				rows.Add(new LowStockReportRow(
					stock.BranchCode,
					stock.ProductSku,
					stock.Quantity,
					threshold));
			}
		}

		return rows
			.OrderBy(row => row.BranchCode.Value, StringComparer.Ordinal)
			.ThenBy(row => row.ProductSku.Value, StringComparer.Ordinal)
			.ToArray();
	}
}
