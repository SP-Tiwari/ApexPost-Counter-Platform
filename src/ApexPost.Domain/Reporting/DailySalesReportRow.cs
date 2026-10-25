
namespace ApexPost.Domain.Reporting;

public sealed record DailySalesReportRow(
	BranchCode BranchCode,
	DateOnly BusinessDate,
	Money RetailSales,
	Money ServiceSales)
{
	public Money TotalSales
	{
		get
		{
			if (!string.Equals(
				RetailSales.Currency,
				ServiceSales.Currency,
				StringComparison.Ordinal))
			{
				throw new InvalidOperationException(
					"Retail and service sales must use the same currency.");
			}

			return new Money(
				RetailSales.Amount + ServiceSales.Amount,
				RetailSales.Currency);
		}
	}
}
