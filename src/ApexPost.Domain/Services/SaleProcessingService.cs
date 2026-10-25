using ApexPost.Domain;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Services;

public sealed class SaleProcessingService
{
	public void ProcessSale(
		TransactionBasket basket,
		BranchCode branchCode,
		IEnumerable<BranchStock> branchStocks)
	{
		ArgumentNullException.ThrowIfNull(basket);
		ArgumentNullException.ThrowIfNull(branchCode);
		ArgumentNullException.ThrowIfNull(branchStocks);

		lock (DomainMutationLock.Gate)
			ProcessSaleUnderLock(basket, branchCode, branchStocks);
	}

	private static void ProcessSaleUnderLock(
		TransactionBasket basket,
		BranchCode branchCode,
		IEnumerable<BranchStock> branchStocks)
	{
		if (basket.PaymentStatus != TransactionPaymentStatus.Open)
			throw new InvalidOperationException(
				"Only an Open transaction basket can be processed as a sale.");

		if (basket.LineItems.Count == 0)
			throw new InvalidOperationException(
				"An empty transaction basket cannot be processed as a sale.");

		basket.EnsureBranch(branchCode);
		_ = basket.Total;

		var requiredQuantities = new Dictionary<Sku, int>();
		foreach (var lineItem in basket.LineItems)
		{
			if (lineItem.Type != TransactionItemType.Goods)
				continue;

			if (lineItem.ProductSku is null)
			{
				throw new InvalidOperationException(
					$"Goods line '{lineItem.Description}' must reference a product SKU to process a sale.");
			}

			requiredQuantities[lineItem.ProductSku] =
				checked(requiredQuantities.GetValueOrDefault(lineItem.ProductSku) + lineItem.Quantity);
		}

		var availableStock = new Dictionary<Sku, BranchStock>();
		foreach (var stock in branchStocks)
		{
			ArgumentNullException.ThrowIfNull(stock);

			if (stock.BranchCode != branchCode ||
				!requiredQuantities.ContainsKey(stock.ProductSku))
			{
				continue;
			}

			if (!availableStock.TryAdd(stock.ProductSku, stock))
			{
				throw new InvalidOperationException(
					$"Multiple stock records exist for product '{stock.ProductSku}' at branch '{branchCode}'.");
			}
		}

		foreach (var (productSku, quantity) in requiredQuantities)
		{
			if (!availableStock.TryGetValue(productSku, out var stock))
			{
				throw new InvalidOperationException(
					$"Product '{productSku}' has no stock record at branch '{branchCode}'.");
			}

			if (stock.Quantity < quantity)
			{
				throw new InvalidOperationException(
					$"Insufficient stock for product '{productSku}' at branch '{branchCode}'.");
			}
		}

		foreach (var (productSku, quantity) in requiredQuantities)
			availableStock[productSku].RemoveStock(quantity);

		basket.AssignBranch(branchCode);
		basket.Pay();
	}
}
