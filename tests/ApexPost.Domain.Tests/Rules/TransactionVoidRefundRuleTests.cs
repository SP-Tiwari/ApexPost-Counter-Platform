using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.Notifications;
using ApexPost.Domain.Rules;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Tests.Rules;

public class TransactionVoidRefundRuleTests
{
	[Fact]
	public void Void_WhenActiveManagerProvidesReason_VoidsOpenBasket()
	{
		var basket = CreateBasket();

		CreateRule().Void(CreateStaff(StaffRole.BranchManager), basket, "Duplicate entry");

		Assert.Equal(TransactionPaymentStatus.Voided, basket.PaymentStatus);
		Assert.Equal(
			"Duplicate entry",
			Assert.IsType<TransactionVoidedNotification>(
				Assert.Single(basket.Notifications)).Reason);
	}

	[Fact]
	public void Refund_WhenActiveManagerProvidesReason_RefundsPaidBasket()
	{
		var basket = CreateBasket();
		basket.Pay();

		CreateRule().Refund(CreateStaff(StaffRole.BranchManager), basket, "Customer request");

		Assert.Equal(TransactionPaymentStatus.Refunded, basket.PaymentStatus);
		Assert.Equal(
			"Customer request",
			Assert.IsType<TransactionRefundedNotification>(
				Assert.Single(basket.Notifications)).Reason);
	}

	[Theory]
	[InlineData(StaffRole.CounterClerk)]
	[InlineData(StaffRole.Auditor)]
	[InlineData(StaffRole.Administrator)]
	public void Void_WhenStaffIsNotManager_RejectsWithoutChangingStatus(StaffRole role)
	{
		var basket = CreateBasket();

		Assert.Throws<InvalidOperationException>(() =>
			CreateRule().Void(CreateStaff(role), basket, "Valid reason"));

		Assert.Equal(TransactionPaymentStatus.Open, basket.PaymentStatus);
		Assert.Empty(basket.Notifications);
	}

	[Theory]
	[InlineData(StaffRole.CounterClerk)]
	[InlineData(StaffRole.Auditor)]
	[InlineData(StaffRole.Administrator)]
	public void Refund_WhenStaffIsNotManager_RejectsWithoutChangingStatus(StaffRole role)
	{
		var basket = CreateBasket();
		basket.Pay();

		Assert.Throws<InvalidOperationException>(() =>
			CreateRule().Refund(CreateStaff(role), basket, "Valid reason"));

		Assert.Equal(TransactionPaymentStatus.Paid, basket.PaymentStatus);
		Assert.Empty(basket.Notifications);
	}

	[Fact]
	public void Void_WhenManagerIsInactive_RejectsWithoutChangingStatus()
	{
		var manager = CreateStaff(StaffRole.BranchManager);
		manager.Deactivate();
		var basket = CreateBasket();

		Assert.Throws<InvalidOperationException>(() =>
			CreateRule().Void(manager, basket, "Valid reason"));

		Assert.Equal(TransactionPaymentStatus.Open, basket.PaymentStatus);
		Assert.Empty(basket.Notifications);
	}

	[Theory]
	[InlineData("")]
	[InlineData(" ")]
	public void Void_WhenReasonIsMissing_RejectsWithoutChangingStatus(string reason)
	{
		var basket = CreateBasket();

		Assert.Throws<ArgumentException>(() =>
			CreateRule().Void(CreateStaff(StaffRole.BranchManager), basket, reason));

		Assert.Equal(TransactionPaymentStatus.Open, basket.PaymentStatus);
		Assert.Empty(basket.Notifications);
	}

	[Theory]
	[InlineData("")]
	[InlineData(" ")]
	public void Refund_WhenReasonIsMissing_RejectsWithoutChangingStatus(string reason)
	{
		var basket = CreateBasket();
		basket.Pay();

		Assert.Throws<ArgumentException>(() =>
			CreateRule().Refund(CreateStaff(StaffRole.BranchManager), basket, reason));

		Assert.Equal(TransactionPaymentStatus.Paid, basket.PaymentStatus);
		Assert.Empty(basket.Notifications);
	}

	[Fact]
	public void Void_WhenBasketIsPaid_RejectsUsingExistingBasketStateRule()
	{
		var basket = CreateBasket();
		basket.Pay();

		Assert.Throws<InvalidOperationException>(() =>
			CreateRule().Void(CreateStaff(StaffRole.BranchManager), basket, "Valid reason"));

		Assert.Equal(TransactionPaymentStatus.Paid, basket.PaymentStatus);
		Assert.Empty(basket.Notifications);
	}

	[Fact]
	public void Refund_WhenBasketIsOpen_RejectsUsingExistingBasketStateRule()
	{
		var basket = CreateBasket();

		Assert.Throws<InvalidOperationException>(() =>
			CreateRule().Refund(CreateStaff(StaffRole.BranchManager), basket, "Valid reason"));

		Assert.Equal(TransactionPaymentStatus.Open, basket.PaymentStatus);
		Assert.Empty(basket.Notifications);
	}

	[Fact]
	public void Refund_WhenBasketIsAlreadyRefunded_RejectsWithoutChangingStatus()
	{
		var basket = CreateBasket();
		basket.Pay();
		CreateRule().Refund(
			CreateStaff(StaffRole.BranchManager),
			basket,
			"Initial refund");

		Assert.Throws<InvalidOperationException>(() =>
			CreateRule().Refund(CreateStaff(StaffRole.BranchManager), basket, "Valid reason"));

		Assert.Equal(TransactionPaymentStatus.Refunded, basket.PaymentStatus);
	}

	[Fact]
	public async Task ConcurrentVoidRequests_ProduceOneTransitionAndOneNotification()
	{
		var basket = CreateBasket();
		var rule = CreateRule();
		var attempts = Enumerable.Range(0, 12)
			.Select(index => Task.Run(() =>
			{
				try
				{
					rule.Void(
						CreateStaff(StaffRole.BranchManager),
						basket,
						$"Reason {index}");
					return true;
				}
				catch (InvalidOperationException)
				{
					return false;
				}
			}))
			.ToArray();

		var succeeded = await Task.WhenAll(attempts);

		Assert.Equal(1, succeeded.Count(result => result));
		Assert.Equal(TransactionPaymentStatus.Voided, basket.PaymentStatus);
		var notification = Assert.IsType<TransactionVoidedNotification>(
			Assert.Single(basket.Notifications));
		Assert.StartsWith("Reason ", notification.Reason);
	}

	[Fact]
	public async Task ConcurrentRefundRequests_ProduceOneTransitionAndOneNotification()
	{
		var basket = CreateBasket();
		basket.Pay();
		var rule = CreateRule();
		var attempts = Enumerable.Range(0, 12)
			.Select(index => Task.Run(() =>
			{
				try
				{
					rule.Refund(
						CreateStaff(StaffRole.BranchManager),
						basket,
						$"Reason {index}");
					return true;
				}
				catch (InvalidOperationException)
				{
					return false;
				}
			}))
			.ToArray();

		var succeeded = await Task.WhenAll(attempts);

		Assert.Equal(1, succeeded.Count(result => result));
		Assert.Equal(TransactionPaymentStatus.Refunded, basket.PaymentStatus);
		var notification = Assert.IsType<TransactionRefundedNotification>(
			Assert.Single(basket.Notifications));
		Assert.StartsWith("Reason ", notification.Reason);
	}

	[Fact]
	public void Void_WithNullStaff_ThrowsArgumentNullExceptionWithoutChangingStatus()
	{
		var basket = CreateBasket();

		Assert.Throws<ArgumentNullException>(() =>
			CreateRule().Void(null!, basket, "Valid reason"));

		Assert.Equal(TransactionPaymentStatus.Open, basket.PaymentStatus);
	}

	[Fact]
	public void Refund_WithNullBasket_ThrowsArgumentNullException()
	{
		Assert.Throws<ArgumentNullException>(() =>
			CreateRule().Refund(CreateStaff(StaffRole.BranchManager), null!, "Valid reason"));
	}

	private static TransactionVoidRefundRule CreateRule() => new();

	private static Staff CreateStaff(StaffRole role) =>
		new("STAFF01", "Staff Member", "staff@example.com", role, new BranchCode("DEL01"));

	private static TransactionBasket CreateBasket()
	{
		var basket = new TransactionBasket(
			"INR",
			new Dictionary<TaxCategory, decimal>
			{
				[TaxCategory.Standard] = 0.1m
			});
		basket.AddLineItem(new TransactionLineItem(
			"Goods",
			TransactionItemType.Goods,
			new Money(10m, "INR"),
			1,
			TaxCategory.Standard));
		return basket;
	}
}
