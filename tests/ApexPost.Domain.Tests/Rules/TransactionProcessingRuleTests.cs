using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.Rules;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Tests.Rules;

public class TransactionProcessingRuleTests
{
	[Fact]
	public void Process_WhenStaffIsInactive_RejectsWithoutPayingBasket()
	{
		var staff = CreateStaff(StaffRole.CounterClerk);
		staff.Deactivate();
		var basket = CreateBasket();

		Assert.Throws<InvalidOperationException>(() =>
			new TransactionProcessingRule().Process(staff, CreateBranch(), basket));

		Assert.Equal(TransactionPaymentStatus.Open, basket.PaymentStatus);
	}

	[Fact]
	public void Process_WhenCounterClerkUsesAssignedBranch_PaysBasket()
	{
		var staff = CreateStaff(StaffRole.CounterClerk);
		var basket = CreateBasket();

		new TransactionProcessingRule().Process(staff, CreateBranch(), basket);

		Assert.Equal(TransactionPaymentStatus.Paid, basket.PaymentStatus);
	}

	[Fact]
	public void Process_WhenCounterClerkUsesDifferentBranch_RejectsWithoutPayingBasket()
	{
		var staff = CreateStaff(StaffRole.CounterClerk);
		var basket = CreateBasket();
		var otherBranch = CreateBranch("MUM01");

		Assert.Throws<InvalidOperationException>(() =>
			new TransactionProcessingRule().Process(staff, otherBranch, basket));

		Assert.Equal(TransactionPaymentStatus.Open, basket.PaymentStatus);
	}

	[Fact]
	public void Process_WhenActiveBranchManagerUsesDifferentBranch_PaysBasket()
	{
		var staff = CreateStaff(StaffRole.BranchManager);
		var basket = CreateBasket();
		var otherBranch = CreateBranch("MUM01");

		new TransactionProcessingRule().Process(staff, otherBranch, basket);

		Assert.Equal(TransactionPaymentStatus.Paid, basket.PaymentStatus);
	}

	[Theory]
	[InlineData(StaffRole.Auditor)]
	[InlineData(StaffRole.Administrator)]
	public void Process_WhenStaffRoleIsNotClerkOrManager_RejectsWithoutPayingBasket(
		StaffRole role)
	{
		var staff = CreateStaff(role);
		var basket = CreateBasket();

		Assert.Throws<InvalidOperationException>(() =>
			new TransactionProcessingRule().Process(staff, CreateBranch(), basket));

		Assert.Equal(TransactionPaymentStatus.Open, basket.PaymentStatus);
	}

	[Fact]
	public void Process_WhenBranchIsInactive_RejectsWithoutPayingBasket()
	{
		var branch = CreateBranch();
		branch.Close();
		var basket = CreateBasket();

		Assert.Throws<InvalidOperationException>(() =>
			new TransactionProcessingRule().Process(
				CreateStaff(StaffRole.CounterClerk),
				branch,
				basket));

		Assert.Equal(TransactionPaymentStatus.Open, basket.PaymentStatus);
	}

	[Fact]
	public void Process_WhenBasketIsEmpty_RejectsAndLeavesBasketOpen()
	{
		var basket = CreateBasket(populate: false);

		Assert.Throws<InvalidOperationException>(() =>
			new TransactionProcessingRule().Process(
				CreateStaff(StaffRole.CounterClerk),
				CreateBranch(),
				basket));

		Assert.Equal(TransactionPaymentStatus.Open, basket.PaymentStatus);
	}

	[Fact]
	public void Process_WithNullStaff_ThrowsArgumentNullException()
	{
		Assert.Throws<ArgumentNullException>(() =>
			new TransactionProcessingRule().Process(
				null!,
				CreateBranch(),
				CreateBasket()));
	}

	[Fact]
	public void Process_WithNullBranch_ThrowsArgumentNullException()
	{
		Assert.Throws<ArgumentNullException>(() =>
			new TransactionProcessingRule().Process(
				CreateStaff(StaffRole.CounterClerk),
				null!,
				CreateBasket()));
	}

	[Fact]
	public void Process_WithNullBasket_ThrowsArgumentNullException()
	{
		Assert.Throws<ArgumentNullException>(() =>
			new TransactionProcessingRule().Process(
				CreateStaff(StaffRole.CounterClerk),
				CreateBranch(),
				null!));
	}

	private static Staff CreateStaff(StaffRole role) =>
		new("STAFF01", "Staff Member", "staff@example.com", role, new BranchCode("DEL01"));

	private static Branch CreateBranch(string code = "DEL01") =>
		new(
			new BranchCode(code),
			"Main Branch",
			"North",
			"09:00-18:00",
			[new CounterTerminal("COUNTER1", "Counter 1")]);

	private static TransactionBasket CreateBasket(bool populate = true)
	{
		var basket = new TransactionBasket(
			"INR",
			new Dictionary<TaxCategory, decimal>
			{
				[TaxCategory.Standard] = 0.1m
			});

		if (populate)
		{
			basket.AddLineItem(new TransactionLineItem(
				"Goods",
				TransactionItemType.Goods,
				new Money(10m, "INR"),
				1,
				TaxCategory.Standard));
		}

		return basket;
	}
}
