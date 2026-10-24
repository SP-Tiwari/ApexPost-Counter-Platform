using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
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
	}

	[Fact]
	public void Refund_WhenActiveManagerProvidesReason_RefundsPaidBasket()
	{
		var basket = CreateBasket();
		basket.Pay();

		CreateRule().Refund(CreateStaff(StaffRole.BranchManager), basket, "Customer request");

		Assert.Equal(TransactionPaymentStatus.Refunded, basket.PaymentStatus);
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
	}

	[Fact]
	public void Void_WhenBasketIsPaid_RejectsUsingExistingBasketStateRule()
	{
		var basket = CreateBasket();
		basket.Pay();

		Assert.Throws<InvalidOperationException>(() =>
			CreateRule().Void(CreateStaff(StaffRole.BranchManager), basket, "Valid reason"));

		Assert.Equal(TransactionPaymentStatus.Paid, basket.PaymentStatus);
	}

	[Fact]
	public void Refund_WhenBasketIsOpen_RejectsUsingExistingBasketStateRule()
	{
		var basket = CreateBasket();

		Assert.Throws<InvalidOperationException>(() =>
			CreateRule().Refund(CreateStaff(StaffRole.BranchManager), basket, "Valid reason"));

		Assert.Equal(TransactionPaymentStatus.Open, basket.PaymentStatus);
	}

	[Fact]
	public void Refund_WhenBasketIsAlreadyRefunded_RejectsWithoutChangingStatus()
	{
		var basket = CreateBasket();
		basket.Pay();
		basket.Refund();

		Assert.Throws<InvalidOperationException>(() =>
			CreateRule().Refund(CreateStaff(StaffRole.BranchManager), basket, "Valid reason"));

		Assert.Equal(TransactionPaymentStatus.Refunded, basket.PaymentStatus);
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
