using ApexPost.Domain;

namespace ApexPost.Domain.Services;

public sealed class SaleReversalService
{
	public void Void(
		TransactionBasket basket,
		string reason,
		IEnumerable<BranchStock> branchStocks)
	{
		ArgumentNullException.ThrowIfNull(basket);
		ArgumentNullException.ThrowIfNull(branchStocks);

		Reverse(basket, reason, branchStocks, basket.Void);
	}

	public void Refund(
		TransactionBasket basket,
		string reason,
		IEnumerable<BranchStock> branchStocks)
	{
		ArgumentNullException.ThrowIfNull(basket);
		ArgumentNullException.ThrowIfNull(branchStocks);

		Reverse(basket, reason, branchStocks, basket.Refund);
	}

	private static void Reverse(
		TransactionBasket basket,
		string reason,
		IEnumerable<BranchStock> branchStocks,
		Action<string> changeStatus)
	{
		lock (DomainMutationLock.Gate)
		{
			// Stock is deducted only when a sale is paid, so only a Paid
			// basket has stock to return; an Open basket never deducted any.
			IReadOnlyList<(BranchStock Stock, int Quantity)> restorations = [];
			if (basket.PaymentStatus == TransactionPaymentStatus.Paid)
			{
				ValidateReason(reason);
				restorations = PrepareRestorations(basket, branchStocks);
			}

			var moneyOrders = PrepareCancellations(basket);
			// The status change validates state and reason before changing
			// anything, so stock and money orders change only if it succeeds.
			changeStatus(reason);
			Restore(restorations);
			foreach (var moneyOrder in moneyOrders)
				moneyOrder.Cancel();
		}
	}

	private static IReadOnlyList<MoneyOrder> PrepareCancellations(
		TransactionBasket basket)
	{
		var moneyOrders = basket.LineItems
			.Select(lineItem => lineItem.MoneyOrder)
			.OfType<MoneyOrder>()
			.ToArray();
		if (moneyOrders.Any(order => order.Status != MoneyOrderStatus.Issued))
			throw new InvalidOperationException(
				"A transaction containing an already cancelled money order cannot be reversed.");

		return moneyOrders;
	}

	private static void ValidateReason(string reason)
	{
		if (string.IsNullOrWhiteSpace(reason))
			throw new ArgumentException(
				"A reason is required to void or refund a transaction.",
				nameof(reason));
	}

	private static IReadOnlyList<(BranchStock Stock, int Quantity)>
		PrepareRestorations(
			TransactionBasket basket,
			IEnumerable<BranchStock> branchStocks)
	{
		var returnedQuantities = new Dictionary<Sku, int>();
		foreach (var lineItem in basket.LineItems)
		{
			// SaleProcessingService rejects goods lines without a SKU, so such
			// a line never deducted stock and has nothing to return.
			if (lineItem.Type != TransactionItemType.Goods ||
				lineItem.ProductSku is null)
			{
				continue;
			}

			returnedQuantities[lineItem.ProductSku] =
				checked(returnedQuantities.GetValueOrDefault(lineItem.ProductSku) + lineItem.Quantity);
		}

		if (returnedQuantities.Count == 0)
			return [];

		var branchCode = basket.BranchCode
			?? throw new InvalidOperationException(
				"A transaction without a recorded branch cannot return stock.");

		var stockRecords = new Dictionary<Sku, BranchStock>();
		foreach (var stock in branchStocks)
		{
			ArgumentNullException.ThrowIfNull(stock);

			if (stock.BranchCode != branchCode ||
				!returnedQuantities.ContainsKey(stock.ProductSku))
			{
				continue;
			}

			if (!stockRecords.TryAdd(stock.ProductSku, stock))
			{
				throw new InvalidOperationException(
					$"Multiple stock records exist for product '{stock.ProductSku}' at branch '{branchCode}'.");
			}
		}

		var restorations = new List<(BranchStock Stock, int Quantity)>(
			returnedQuantities.Count);
		foreach (var (productSku, quantity) in returnedQuantities)
		{
			if (!stockRecords.TryGetValue(productSku, out var stock))
			{
				throw new InvalidOperationException(
					$"Product '{productSku}' has no stock record at branch '{branchCode}' to return stock to.");
			}

			// Check for overflow before any stock record is changed.
			_ = checked(stock.Quantity + quantity);
			restorations.Add((stock, quantity));
		}

		return restorations;
	}

	private static void Restore(
		IReadOnlyList<(BranchStock Stock, int Quantity)> restorations)
	{
		foreach (var (stock, quantity) in restorations)
			stock.AddStock(quantity);
	}
}
