using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Tests.Entities;

public class TransactionBasketPaymentStatusTests
{
	[Fact]
	public void Pay_WhenOpenAndNonEmpty_ChangesStatusToPaid()
	{
		var basket = CreatePopulatedBasket();

		basket.Pay();

		Assert.Equal(TransactionPaymentStatus.Paid, basket.PaymentStatus);
	}

	[Fact]
	public void Void_WhenOpen_ChangesStatusToVoided()
	{
		var basket = CreatePopulatedBasket();

		basket.Void();

		Assert.Equal(TransactionPaymentStatus.Voided, basket.PaymentStatus);
	}

	[Fact]
	public void Refund_WhenPaid_ChangesStatusToRefunded()
	{
		var basket = CreatePopulatedBasket();
		basket.Pay();

		basket.Refund();

		Assert.Equal(TransactionPaymentStatus.Refunded, basket.PaymentStatus);
	}

	[Fact]
	public void Pay_WhenAlreadyPaid_ThrowsInvalidOperationException()
	{
		var basket = CreatePopulatedBasket();
		basket.Pay();

		Assert.Throws<InvalidOperationException>(() => basket.Pay());
	}

	[Fact]
	public void Void_WhenAlreadyVoided_ThrowsInvalidOperationException()
	{
		var basket = CreatePopulatedBasket();
		basket.Void();

		Assert.Throws<InvalidOperationException>(() => basket.Void());
	}

	[Fact]
	public void Void_WhenAlreadyPaid_ThrowsInvalidOperationException()
	{
		var basket = CreatePopulatedBasket();
		basket.Pay();

		Assert.Throws<InvalidOperationException>(() => basket.Void());
	}

	[Fact]
	public void Refund_WhenOpen_ThrowsInvalidOperationException()
	{
		var basket = CreatePopulatedBasket();

		Assert.Throws<InvalidOperationException>(() => basket.Refund());
	}

	[Fact]
	public void Refund_WhenAlreadyRefunded_ThrowsInvalidOperationException()
	{
		var basket = CreatePopulatedBasket();
		basket.Pay();
		basket.Refund();

		Assert.Throws<InvalidOperationException>(() => basket.Refund());
	}

	[Fact]
	public void Pay_WhenVoided_ThrowsInvalidOperationException()
	{
		var basket = CreatePopulatedBasket();
		basket.Void();

		Assert.Throws<InvalidOperationException>(() => basket.Pay());
	}

	[Fact]
	public void Void_WhenRefunded_ThrowsInvalidOperationException()
	{
		var basket = CreatePopulatedBasket();
		basket.Pay();
		basket.Refund();

		Assert.Throws<InvalidOperationException>(() => basket.Void());
	}

	private static TransactionBasket CreatePopulatedBasket()
	{
		var basket = new TransactionBasket(
			"INR",
			new Dictionary<TaxCategory, decimal>
			{
				[TaxCategory.Standard] = 0.1m
			});

		basket.AddLineItem(new TransactionLineItem(
			"Sample item",
			TransactionItemType.Goods,
			new Money(10m, "INR"),
			1,
			TaxCategory.Standard));

		return basket;
	}
}
