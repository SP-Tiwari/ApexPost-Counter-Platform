
namespace ApexPost.Domain.Reporting;

public sealed class DailySalesReportService
{
	public IReadOnlyList<DailySalesReportRow> CreateReport(
		IEnumerable<DailySalesReportEntry> entries)
	{
		ArgumentNullException.ThrowIfNull(entries);

		var totals = new Dictionary<
			(BranchCode BranchCode, DateOnly BusinessDate),
			(Money RetailSales, Money ServiceSales)>();

		foreach (var entry in entries)
		{
			ArgumentNullException.ThrowIfNull(entry);
			var sales = entry.GetPaidSalesTotals();
			if (sales is null)
				continue;

			var key = (entry.BranchCode, entry.BusinessDate);
			if (!totals.TryGetValue(key, out var current))
			{
				totals.Add(key, (sales.Value.RetailSales, sales.Value.ServiceSales));
				continue;
			}

			if (!string.Equals(
				current.RetailSales.Currency,
				sales.Value.RetailSales.Currency,
				StringComparison.Ordinal))
			{
				throw new InvalidOperationException(
					$"Daily sales for branch '{entry.BranchCode}' on '{entry.BusinessDate}' contain incompatible currencies.");
			}

			totals[key] = (
				new Money(
					current.RetailSales.Amount + sales.Value.RetailSales.Amount,
					current.RetailSales.Currency),
				new Money(
					current.ServiceSales.Amount + sales.Value.ServiceSales.Amount,
					current.ServiceSales.Currency));
		}

		return totals
			.Select(pair => new DailySalesReportRow(
				pair.Key.BranchCode,
				pair.Key.BusinessDate,
				pair.Value.RetailSales,
				pair.Value.ServiceSales))
			.OrderBy(row => row.BusinessDate)
			.ThenBy(row => row.BranchCode.Value, StringComparer.Ordinal)
			.ToArray();
	}
}
