using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Tests.Entities;

public class TransactionBasketTests
{
	[Fact]
	public void Basket_WithGoodsAndServices_CalculatesSubtotalTaxAndTotal()
	{
		var basket = CreateBasket();
		basket.AddLineItem(CreateLine(
			"Packaging box",
			TransactionItemType.Goods,
			10m,
			2,
			TaxCategory.Standard));
		basket.AddLineItem(CreateLine(
			"Shipping",
			TransactionItemType.Service,
			20m,
			1,
			TaxCategory.Reduced));

		Assert.Equal(2, basket.LineItems.Count);
		Assert.Contains(basket.LineItems, item => item.Type == TransactionItemType.Goods);
		Assert.Contains(basket.LineItems, item => item.Type == TransactionItemType.Service);
		Assert.Equal(new Money(40m, "INR"), basket.Subtotal);
		Assert.Equal(new Money(3m, "INR"), basket.Tax);
		Assert.Equal(new Money(43m, "INR"), basket.Total);
	}

	[Fact]
	public void Basket_WithZeroRatedCategory_DoesNotAddTax()
	{
		var basket = CreateBasket();
		basket.AddLineItem(CreateLine(
			"Zero-rated item",
			TransactionItemType.Goods,
			25m,
			1,
			TaxCategory.ZeroRated));

		Assert.Equal(new Money(25m, "INR"), basket.Subtotal);
		Assert.Equal(new Money(0m, "INR"), basket.Tax);
		Assert.Equal(new Money(25m, "INR"), basket.Total);
	}

	[Fact]
	public void AddLineItem_WithCurrencyMismatch_ThrowsArgumentException()
	{
		var basket = CreateBasket();

		Assert.Throws<ArgumentException>(() =>
			basket.AddLineItem(new TransactionLineItem(
				"Foreign currency item",
				TransactionItemType.Goods,
				new Money(10m, "USD"),
				1,
				TaxCategory.Standard)));
	}

	[Fact]
	public void AddLineItem_WithUnconfiguredTaxCategory_ThrowsInvalidOperationException()
	{
		var basket = new TransactionBasket(
			"INR",
			new Dictionary<TaxCategory, decimal>
			{
				[TaxCategory.Standard] = 0.1m
			});

		Assert.Throws<InvalidOperationException>(() =>
			basket.AddLineItem(CreateLine(
				"Reduced-category item",
				TransactionItemType.Goods,
				10m,
				1,
				TaxCategory.Reduced)));
	}

	[Fact]
	public void Pay_WhenBasketIsEmpty_ThrowsAndKeepsBasketOpen()
	{
		var basket = CreateBasket();

		Assert.Throws<InvalidOperationException>(() => basket.Pay());

		Assert.Equal(TransactionPaymentStatus.Open, basket.PaymentStatus);
	}

	[Fact]
	public void Constructor_WithInvalidCurrency_ThrowsArgumentException()
	{
		Assert.Throws<ArgumentException>(() =>
			new TransactionBasket(
				"IN",
				new Dictionary<TaxCategory, decimal>()));
	}

	[Fact]
	public void Constructor_WithNullTaxConfiguration_ThrowsArgumentNullException()
	{
		Assert.Throws<ArgumentNullException>(() =>
			new TransactionBasket("INR", null!));
	}

	[Fact]
	public void Constructor_WithNegativeTaxRate_ThrowsArgumentOutOfRangeException()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() =>
			new TransactionBasket(
				"INR",
				new Dictionary<TaxCategory, decimal>
				{
					[TaxCategory.Standard] = -0.1m
				}));
	}

	[Fact]
	public void AddLineItem_AfterPayment_ThrowsInvalidOperationException()
	{
		var basket = CreateBasket();
		basket.AddLineItem(CreateLine(
			"Item",
			TransactionItemType.Goods,
			10m,
			1,
			TaxCategory.Standard));
		basket.Pay();

		Assert.Throws<InvalidOperationException>(() =>
			basket.AddLineItem(CreateLine(
				"Another item",
				TransactionItemType.Goods,
				5m,
				1,
				TaxCategory.Standard)));
	}

	private static TransactionBasket CreateBasket() =>
		new(
			"INR",
			new Dictionary<TaxCategory, decimal>
			{
				[TaxCategory.Standard] = 0.1m,
				[TaxCategory.Reduced] = 0.05m,
				[TaxCategory.ZeroRated] = 0m
			});

	private static TransactionLineItem CreateLine(
		string description,
		TransactionItemType type,
		decimal unitPrice,
		int quantity,
		TaxCategory taxCategory) =>
		new(
			description,
			type,
			new Money(unitPrice, "INR"),
			quantity,
			taxCategory);
}
