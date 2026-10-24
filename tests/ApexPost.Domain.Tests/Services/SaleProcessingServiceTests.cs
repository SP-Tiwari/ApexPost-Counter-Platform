using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.Services;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Tests.Services;

public class SaleProcessingServiceTests
{
	[Fact]
	public void ProcessSale_WithGoodsAndService_DeductsGoodsStockAndPaysBasket()
	{
		var stock = CreateStock("DEL01", "BOX01", 5);
		var basket = CreateBasket();
		basket.AddLineItem(CreateProductLine("BOX01", "Box", 12m, 2));
		basket.AddLineItem(CreateServiceLine());

		CreateService().ProcessSale(basket, new BranchCode("DEL01"), [stock]);

		Assert.Equal(3, stock.Quantity);
		Assert.Equal(TransactionPaymentStatus.Paid, basket.PaymentStatus);
	}

	[Fact]
	public void ProcessSale_WithMultipleLinesForSameProduct_DeductsAggregatedQuantity()
	{
		var stock = CreateStock("DEL01", "BOX01", 5);
		var basket = CreateBasket();
		basket.AddLineItem(CreateProductLine("BOX01", "Box", 12m, 2));
		basket.AddLineItem(CreateProductLine("BOX01", "Box", 12m, 1));

		CreateService().ProcessSale(basket, new BranchCode("DEL01"), [stock]);

		Assert.Equal(2, stock.Quantity);
		Assert.Equal(TransactionPaymentStatus.Paid, basket.PaymentStatus);
	}

	[Fact]
	public void ProcessSale_WhenStockIsInsufficient_LeavesAllStockAndBasketUnchanged()
	{
		var firstStock = CreateStock("DEL01", "BOX01", 5);
		var secondStock = CreateStock("DEL01", "PEN01", 1);
		var basket = CreateBasket();
		basket.AddLineItem(CreateProductLine("BOX01", "Box", 12m, 2));
		basket.AddLineItem(CreateProductLine("PEN01", "Pen", 3m, 2));

		Assert.Throws<InvalidOperationException>(() =>
			CreateService().ProcessSale(
				basket,
				new BranchCode("DEL01"),
				[firstStock, secondStock]));

		Assert.Equal(5, firstStock.Quantity);
		Assert.Equal(1, secondStock.Quantity);
		Assert.Equal(TransactionPaymentStatus.Open, basket.PaymentStatus);
	}

	[Fact]
	public void ProcessSale_WhenAggregateQuantityOverflows_LeavesStateUnchanged()
	{
		var stock = CreateStock("DEL01", "BOX01", int.MaxValue);
		var basket = CreateBasket();
		basket.AddLineItem(CreateProductLine("BOX01", "Box", 0m, int.MaxValue));
		basket.AddLineItem(CreateProductLine("BOX01", "Box", 0m, 1));

		Assert.Throws<OverflowException>(() =>
			CreateService().ProcessSale(basket, new BranchCode("DEL01"), [stock]));

		Assert.Equal(int.MaxValue, stock.Quantity);
		Assert.Equal(TransactionPaymentStatus.Open, basket.PaymentStatus);
	}

	[Fact]
	public void ProcessSale_WhenBasketTotalOverflows_LeavesStateUnchanged()
	{
		var stock = CreateStock("DEL01", "BOX01", 1);
		var basket = CreateBasket();
		basket.AddLineItem(CreateProductLine("BOX01", "Box", decimal.MaxValue, 1));

		Assert.Throws<OverflowException>(() =>
			CreateService().ProcessSale(basket, new BranchCode("DEL01"), [stock]));

		Assert.Equal(1, stock.Quantity);
		Assert.Equal(TransactionPaymentStatus.Open, basket.PaymentStatus);
	}

	[Fact]
	public void ProcessSale_WhenStockExistsAtAnotherBranch_LeavesStateUnchanged()
	{
		var stock = CreateStock("MUM01", "BOX01", 5);
		var basket = CreateBasket();
		basket.AddLineItem(CreateProductLine("BOX01", "Box", 12m, 2));

		Assert.Throws<InvalidOperationException>(() =>
			CreateService().ProcessSale(basket, new BranchCode("DEL01"), [stock]));

		Assert.Equal(5, stock.Quantity);
		Assert.Equal(TransactionPaymentStatus.Open, basket.PaymentStatus);
	}

	[Fact]
	public void ProcessSale_WhenProductSkuDoesNotMatchStock_LeavesStateUnchanged()
	{
		var stock = CreateStock("DEL01", "PEN01", 5);
		var basket = CreateBasket();
		basket.AddLineItem(CreateProductLine("BOX01", "Box", 12m, 2));

		Assert.Throws<InvalidOperationException>(() =>
			CreateService().ProcessSale(basket, new BranchCode("DEL01"), [stock]));

		Assert.Equal(5, stock.Quantity);
		Assert.Equal(TransactionPaymentStatus.Open, basket.PaymentStatus);
	}

	[Fact]
	public void ProcessSale_WhenGoodsLineHasNoProductSku_RejectsBeforeChangingState()
	{
		var stock = CreateStock("DEL01", "BOX01", 5);
		var basket = CreateBasket();
		basket.AddLineItem(new TransactionLineItem(
			"Unmatched goods",
			TransactionItemType.Goods,
			new Money(12m, "INR"),
			1,
			TaxCategory.Standard));

		Assert.Throws<InvalidOperationException>(() =>
			CreateService().ProcessSale(basket, new BranchCode("DEL01"), [stock]));

		Assert.Equal(5, stock.Quantity);
		Assert.Equal(TransactionPaymentStatus.Open, basket.PaymentStatus);
	}

	[Fact]
	public void ProcessSale_WithServiceOnlyBasketDoesNotChangeStock()
	{
		var stock = CreateStock("DEL01", "BOX01", 5);
		var basket = CreateBasket();
		basket.AddLineItem(CreateServiceLine());

		CreateService().ProcessSale(basket, new BranchCode("DEL01"), [stock]);

		Assert.Equal(5, stock.Quantity);
		Assert.Equal(TransactionPaymentStatus.Paid, basket.PaymentStatus);
	}

	[Fact]
	public void ProcessSale_WhenBasketIsNotOpen_LeavesStockUnchanged()
	{
		var stock = CreateStock("DEL01", "BOX01", 5);
		var basket = CreateBasket();
		basket.AddLineItem(CreateProductLine("BOX01", "Box", 12m, 2));
		basket.Pay();

		Assert.Throws<InvalidOperationException>(() =>
			CreateService().ProcessSale(basket, new BranchCode("DEL01"), [stock]));

		Assert.Equal(5, stock.Quantity);
		Assert.Equal(TransactionPaymentStatus.Paid, basket.PaymentStatus);
	}

	[Fact]
	public void ProcessSale_WithEmptyBasket_LeavesStockUnchanged()
	{
		var stock = CreateStock("DEL01", "BOX01", 5);

		Assert.Throws<InvalidOperationException>(() =>
			CreateService().ProcessSale(
				CreateBasket(),
				new BranchCode("DEL01"),
				[stock]));

		Assert.Equal(5, stock.Quantity);
	}

	[Fact]
	public void ProcessSale_WithDuplicateStockRecords_RejectsWithoutChangingStock()
	{
		var firstStock = CreateStock("DEL01", "BOX01", 5);
		var duplicateStock = CreateStock("DEL01", "BOX01", 5);
		var basket = CreateBasket();
		basket.AddLineItem(CreateProductLine("BOX01", "Box", 12m, 2));

		Assert.Throws<InvalidOperationException>(() =>
			CreateService().ProcessSale(
				basket,
				new BranchCode("DEL01"),
				[firstStock, duplicateStock]));

		Assert.Equal(5, firstStock.Quantity);
		Assert.Equal(5, duplicateStock.Quantity);
		Assert.Equal(TransactionPaymentStatus.Open, basket.PaymentStatus);
	}

	[Fact]
	public void ProcessSale_WithMissingArguments_ThrowsArgumentNullException()
	{
		var service = CreateService();
		var basket = CreateBasket();
		var branchCode = new BranchCode("DEL01");

		Assert.Throws<ArgumentNullException>(() =>
			service.ProcessSale(null!, branchCode, []));
		Assert.Throws<ArgumentNullException>(() =>
			service.ProcessSale(basket, null!, []));
		Assert.Throws<ArgumentNullException>(() =>
			service.ProcessSale(basket, branchCode, null!));
	}

	[Fact]
	public async Task ProcessSale_WhenSalesRunConcurrently_DoesNotPartiallyDeductStock()
	{
		const int availableQuantity = 16;
		const int numberOfSales = availableQuantity * 2;
		var stockA = CreateStock("DEL01", "BOX01", availableQuantity);
		var stockB = CreateStock("DEL01", "PEN01", availableQuantity);
		var service = CreateService();
		var start = new TaskCompletionSource(
			TaskCreationOptions.RunContinuationsAsynchronously);

		var baskets = Enumerable.Range(0, numberOfSales)
			.Select(index => CreateBasketWithTwoProducts(reverseOrder: index % 2 == 1))
			.ToArray();

		var sales = baskets.Select(basket => Task.Run(async () =>
		{
			await start.Task;
			try
			{
				service.ProcessSale(
					basket,
					new BranchCode("DEL01"),
					[stockA, stockB]);
				return true;
			}
			catch (InvalidOperationException)
			{
				return false;
			}
		})).ToArray();

		start.SetResult();
		var completedSales = await Task.WhenAll(sales);

		Assert.Equal(availableQuantity, completedSales.Count(succeeded => succeeded));
		Assert.Equal(0, stockA.Quantity);
		Assert.Equal(0, stockB.Quantity);
		Assert.Equal(
			availableQuantity,
			baskets.Count(basket =>
				basket.PaymentStatus == TransactionPaymentStatus.Paid));
		Assert.Equal(
			numberOfSales - availableQuantity,
			baskets.Count(basket =>
				basket.PaymentStatus == TransactionPaymentStatus.Open));
	}

	private static SaleProcessingService CreateService() => new();

	private static BranchStock CreateStock(
		string branchCode,
		string sku,
		int quantity) =>
		new(new BranchCode(branchCode), new Sku(sku), quantity);

	private static Product CreateProduct(string sku, string name, decimal price) =>
		new(
			new Sku(sku),
			name,
			ProductCategory.Packaging,
			new Money(price, "INR"),
			TaxCategory.Standard);

	private static TransactionLineItem CreateProductLine(
		string sku,
		string name,
		decimal price,
		int quantity) =>
		new(CreateProduct(sku, name, price), quantity);

	private static TransactionLineItem CreateServiceLine() =>
		new(
			"Shipping",
			TransactionItemType.Service,
			new Money(20m, "INR"),
			1,
			TaxCategory.Standard);

	private static TransactionBasket CreateBasket() =>
		new(
			"INR",
			new Dictionary<TaxCategory, decimal>
			{
				[TaxCategory.Standard] = 0.1m
			});

	private static TransactionBasket CreateBasketWithTwoProducts(bool reverseOrder)
	{
		var basket = CreateBasket();
		var firstLine = CreateProductLine("BOX01", "Box", 1m, 1);
		var secondLine = CreateProductLine("PEN01", "Pen", 1m, 1);

		if (reverseOrder)
		{
			basket.AddLineItem(secondLine);
			basket.AddLineItem(firstLine);
		}
		else
		{
			basket.AddLineItem(firstLine);
			basket.AddLineItem(secondLine);
		}

		return basket;
	}
}
