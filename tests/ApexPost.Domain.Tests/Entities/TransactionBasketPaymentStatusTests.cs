
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

		Void(basket);

		Assert.Equal(TransactionPaymentStatus.Voided, basket.PaymentStatus);
		Assert.IsType<TransactionVoidedNotification>(
			Assert.Single(basket.Notifications));
	}

	[Fact]
	public void Refund_WhenPaid_ChangesStatusToRefunded()
	{
		var basket = CreatePopulatedBasket();
		basket.Pay();

		Refund(basket);

		Assert.Equal(TransactionPaymentStatus.Refunded, basket.PaymentStatus);
		Assert.IsType<TransactionRefundedNotification>(
			Assert.Single(basket.Notifications));
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
		Void(basket);
		var notificationsBeforeRepeat = basket.Notifications;

		Assert.Throws<InvalidOperationException>(() => Void(basket, "Repeated void"));
		Assert.Equal(notificationsBeforeRepeat, basket.Notifications);
	}

	[Fact]
	public void Void_WhenAlreadyPaid_ThrowsInvalidOperationException()
	{
		var basket = CreatePopulatedBasket();
		basket.Pay();

		Assert.Throws<InvalidOperationException>(() => Void(basket, "Invalid state"));
	}

	[Fact]
	public void Refund_WhenOpen_ThrowsInvalidOperationException()
	{
		var basket = CreatePopulatedBasket();

		Assert.Throws<InvalidOperationException>(() => Refund(basket, "Invalid state"));
		Assert.Empty(basket.Notifications);
	}

	[Fact]
	public void Refund_WhenAlreadyRefunded_ThrowsInvalidOperationException()
	{
		var basket = CreatePopulatedBasket();
		basket.Pay();
		Refund(basket);
		var notificationsBeforeRepeat = basket.Notifications;

		Assert.Throws<InvalidOperationException>(() => Refund(basket, "Repeated refund"));
		Assert.Equal(notificationsBeforeRepeat, basket.Notifications);
	}

	[Fact]
	public void Pay_WhenVoided_ThrowsInvalidOperationException()
	{
		var basket = CreatePopulatedBasket();
		Void(basket);

		Assert.Throws<InvalidOperationException>(() => basket.Pay());
	}

	[Fact]
	public void Void_WhenRefunded_ThrowsInvalidOperationException()
	{
		var basket = CreatePopulatedBasket();
		basket.Pay();
		Refund(basket);

		Assert.Throws<InvalidOperationException>(() => Void(basket, "Invalid state"));
	}

	private static TransactionBasket CreatePopulatedBasket()
	{
		var basket = new TransactionBasket(
			"INR",
			new Dictionary<TaxCategory, decimal>
			{
				[TaxCategory.Standard] = 0.1m
			},
			new BranchCode("DEL01"));

		basket.AddLineItem(new TransactionLineItem(
			"Sample item",
			TransactionItemType.Goods,
			new Money(10m, "INR"),
			1,
			TaxCategory.Standard));

		return basket;
	}

	private static void Void(TransactionBasket basket, string reason = "Test void reason") =>
		new TransactionVoidRefundRule().Void(CreateBranchManager(), basket, reason);

	private static void Refund(TransactionBasket basket, string reason = "Test refund reason") =>
		new TransactionVoidRefundRule().Refund(CreateBranchManager(), basket, reason);

	private static Staff CreateBranchManager() =>
		new(
			"MANAGER01",
			"Branch Manager",
			"manager@example.com",
			StaffRole.BranchManager,
			new BranchCode("DEL01"));
}
