
namespace ApexPost.Domain.Reporting;

public sealed record DailySalesReportEntry
{
	private readonly TransactionBasket _basket;

	public BranchCode BranchCode { get; }
	public DateOnly BusinessDate { get; }

	public DailySalesReportEntry(
		BranchCode branchCode,
		DateOnly businessDate,
		TransactionBasket basket)
	{
		ArgumentNullException.ThrowIfNull(branchCode);
		ArgumentNullException.ThrowIfNull(basket);

		if (basket.PaymentStatus != TransactionPaymentStatus.Paid)
			throw new InvalidOperationException(
				"Daily sales report entries require a Paid transaction basket.");

		BranchCode = branchCode;
		BusinessDate = businessDate;
		_basket = basket;
	}

	internal (Money RetailSales, Money ServiceSales)? GetPaidSalesTotals() =>
		_basket.GetPaidSalesTotals();
}
