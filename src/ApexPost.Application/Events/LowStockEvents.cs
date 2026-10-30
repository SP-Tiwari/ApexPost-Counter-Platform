using ApexPost.Domain.Events;
using ApexPost.Domain.Notifications;

namespace ApexPost.Application.Events;

internal static class LowStockEvents
{
	private static readonly LowStockNotificationRule Rule = new();

	// Records the quantities of the given products at a branch before a
	// change, so Evaluate can tell which ones crossed the threshold.
	public static IReadOnlyDictionary<Sku, int> CaptureQuantities(
		IEnumerable<BranchStock> branchStocks,
		BranchCode branchCode,
		IEnumerable<Sku> skus)
	{
		var wanted = skus.ToHashSet();
		var quantities = new Dictionary<Sku, int>();
		foreach (var stock in branchStocks)
		{
			if (stock.BranchCode == branchCode && wanted.Contains(stock.ProductSku))
				quantities[stock.ProductSku] = stock.Quantity;
		}

		return quantities;
	}

	// Raises an event only for stock that was above the threshold before the
	// change and is at or below it afterwards (the existing low-stock rule),
	// so a product that stays low is not reported again on every sale.
	// Without a configured threshold no event is raised.
	public static IReadOnlyList<StockLowEvent> Evaluate(
		IEnumerable<BranchStock> branchStocks,
		BranchCode branchCode,
		IReadOnlyDictionary<Sku, int> quantitiesBefore,
		int? threshold,
		DateTimeOffset occurredAtUtc)
	{
		if (threshold is not { } configuredThreshold)
			return [];

		return branchStocks
			.Where(stock =>
				stock.BranchCode == branchCode &&
				quantitiesBefore.TryGetValue(stock.ProductSku, out var before) &&
				before > configuredThreshold)
			.Select(stock => Rule.Evaluate(stock, configuredThreshold))
			.OfType<LowStockNotification>()
			.Select(notification =>
				new StockLowEvent(notification, occurredAtUtc))
			.ToArray();
	}
}
