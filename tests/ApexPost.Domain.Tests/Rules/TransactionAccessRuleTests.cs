namespace ApexPost.Domain.Tests.Rules;

public sealed class TransactionAccessRuleTests
{
	private static readonly BranchCode HomeBranch = new("BR01");
	private static readonly BranchCode OtherBranch = new("BR02");
	private readonly TransactionAccessRule _rule = new();

	[Fact]
	public void Manager_CanAccessAndViewOnlyHomeBranchTransactions()
	{
		var manager = CreateStaff("manager-1", StaffRole.BranchManager);

		Assert.True(_rule.CanAccess(manager, CreateSale(HomeBranch, "clerk-1")));
		Assert.True(_rule.CanView(manager, CreateSale(HomeBranch, "clerk-1")));
		Assert.False(_rule.CanAccess(manager, CreateSale(OtherBranch, "clerk-1")));
		Assert.False(_rule.CanView(manager, CreateSale(OtherBranch, "clerk-1")));
		Assert.False(_rule.CanView(manager, CreateSale(null, "clerk-1")));
	}

	[Fact]
	public void Clerk_CanViewOwnSaleButNeverChangeIt()
	{
		var clerk = CreateStaff("clerk-1", StaffRole.CounterClerk);
		var ownSale = CreateSale(HomeBranch, "clerk-1");

		Assert.True(_rule.CanView(clerk, ownSale));
		Assert.False(_rule.CanAccess(clerk, ownSale));
		Assert.Throws<InvalidOperationException>(
			() => _rule.EnsureCanAccess(clerk, ownSale));
	}

	[Theory]
	[InlineData("clerk-2", "BR01")]
	[InlineData("clerk-1", "BR02")]
	[InlineData(null, "BR01")]
	public void Clerk_CannotViewOtherSales(string? recordedClerk, string branch)
	{
		var clerk = CreateStaff("clerk-1", StaffRole.CounterClerk);

		Assert.False(_rule.CanView(clerk, CreateSale(new BranchCode(branch), recordedClerk)));
	}

	[Fact]
	public void InactiveStaff_CannotView()
	{
		var clerk = CreateStaff("clerk-1", StaffRole.CounterClerk);
		var manager = CreateStaff("manager-1", StaffRole.BranchManager);
		clerk.Deactivate();
		manager.Deactivate();

		Assert.False(_rule.CanView(clerk, CreateSale(HomeBranch, "clerk-1")));
		Assert.False(_rule.CanView(manager, CreateSale(HomeBranch, "clerk-1")));
	}

	[Theory]
	[InlineData(StaffRole.Auditor)]
	[InlineData(StaffRole.Administrator)]
	public void OtherRoles_CannotViewEvenWhenRecordedAsClerk(StaffRole role)
	{
		var staff = CreateStaff("staff-1", role);

		Assert.False(_rule.CanView(staff, CreateSale(HomeBranch, "staff-1")));
	}

	private static Staff CreateStaff(string id, StaffRole role) =>
		new(id, id, $"{id}@example.test", role, HomeBranch);

	private static TransactionBasket CreateSale(BranchCode? branch, string? clerkId) =>
		TransactionBasket.Reconstitute(
			Guid.NewGuid(),
			"INR",
			new Dictionary<TaxCategory, decimal> { [TaxCategory.Standard] = 0m },
			[new TransactionLineItem(
				"Counter service",
				TransactionItemType.Service,
				new Money(10m, "INR"),
				1,
				TaxCategory.Standard)],
			TransactionPaymentStatus.Paid,
			[],
			branch,
			DateTimeOffset.UnixEpoch,
			clerkId is null ? null : new CounterSaleDetails("COUNTER-1", clerkId, "Cash"));
}
