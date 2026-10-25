
namespace ApexPost.Application.Persistence;

public sealed class ApexPostSnapshot
{
	public IReadOnlyList<Branch> Branches { get; }
	public IReadOnlyList<Staff> Staff { get; }
	public IReadOnlyList<Product> Products { get; }
	public IReadOnlyList<BranchStock> BranchStocks { get; }
	public IReadOnlyList<ShippingItem> ShippingItems { get; }
	public IReadOnlyList<ShippingRate> ShippingRates { get; }
	public IReadOnlyList<MoneyOrder> MoneyOrders { get; }
	public IReadOnlyList<IdentityVerification> IdentityVerifications { get; }
	public IReadOnlyList<TransactionBasket> TransactionBaskets { get; }

	public ApexPostSnapshot(
		IEnumerable<Branch> branches,
		IEnumerable<Staff> staff,
		IEnumerable<Product> products,
		IEnumerable<BranchStock> branchStocks,
		IEnumerable<ShippingItem> shippingItems,
		IEnumerable<ShippingRate> shippingRates,
		IEnumerable<MoneyOrder> moneyOrders,
		IEnumerable<IdentityVerification> identityVerifications,
		IEnumerable<TransactionBasket> transactionBaskets)
	{
		Branches = Copy(branches, nameof(branches));
		Staff = Copy(staff, nameof(staff));
		Products = Copy(products, nameof(products));
		BranchStocks = Copy(branchStocks, nameof(branchStocks));
		ShippingItems = Copy(shippingItems, nameof(shippingItems));
		ShippingRates = Copy(shippingRates, nameof(shippingRates));
		MoneyOrders = Copy(moneyOrders, nameof(moneyOrders));
		IdentityVerifications = Copy(
			identityVerifications, nameof(identityVerifications));
		TransactionBaskets = Copy(transactionBaskets, nameof(transactionBaskets));
	}

	public static ApexPostSnapshot Empty { get; } = new(
		[], [], [], [], [], [], [], [], []);

	private static IReadOnlyList<T> Copy<T>(
		IEnumerable<T> values,
		string parameterName) where T : class
	{
		ArgumentNullException.ThrowIfNull(values, parameterName);
		var copy = values.ToArray();

		if (copy.Any(value => value is null))
			throw new ArgumentException(
				"Snapshot collections cannot contain null values.", parameterName);

		return Array.AsReadOnly(copy);
	}
}
