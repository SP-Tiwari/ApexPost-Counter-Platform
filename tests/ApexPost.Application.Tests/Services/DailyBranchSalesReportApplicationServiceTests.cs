using ApexPost.Domain.Reporting;
using NSubstitute;

namespace ApexPost.Application.Tests.Services;

public sealed class DailyBranchSalesReportApplicationServiceTests
{
	private static readonly BranchCode HomeBranch = new("BR01");
	private static readonly BranchCode OtherBranch = new("BR02");
	private const string ManagerId = "manager-1";
	private static readonly DateOnly ReportDate = new(2026, 1, 15);

	[Fact]
	public async Task GetReportAsync_ReturnsDailyRetailAndServiceTotalsForHomeBranch()
	{
		var basket = CreatePaidBasket(
			HomeBranch,
			goodsTotal: 100m,
			serviceTotal: 30m,
			currency: "INR");
		var foreignBasket = CreatePaidBasket(
			OtherBranch,
			goodsTotal: 70m,
			serviceTotal: 0m,
			currency: "INR");
		var service = CreateService(
			CreateStaff(ManagerId, StaffRole.BranchManager, HomeBranch),
			[basket, foreignBasket]);

		var report = await service.GetReportAsync(ManagerId, ReportDate);

		Assert.Equal(
			[
				new DailySalesReportRow(
					HomeBranch,
					ReportDate,
					new Money(100m, "INR"),
					new Money(30m, "INR"))
			],
			report);
	}

	[Fact]
	public async Task GetReportAsync_WhenNoPaidHomeTransactions_ReturnsEmpty()
	{
		var openBasket = CreateOpenBasket(HomeBranch, currency: "INR");
		var service = CreateService(
			CreateStaff(ManagerId, StaffRole.BranchManager, HomeBranch),
			[openBasket]);

		Assert.Empty(await service.GetReportAsync(ManagerId, ReportDate));
	}

	[Theory]
	[InlineData(StaffRole.CounterClerk)]
	[InlineData(StaffRole.Auditor)]
	[InlineData(StaffRole.Administrator)]
	public async Task GetReportAsync_NonManagersAreDenied(StaffRole role)
	{
		var service = CreateService(
			CreateStaff(ManagerId, role, HomeBranch),
			[]);

		await Assert.ThrowsAsync<UnauthorizedAccessException>(
			() => service.GetReportAsync(ManagerId, ReportDate));
	}

	[Fact]
	public async Task GetReportAsync_InactiveManagerIsDenied()
	{
		var manager = CreateStaff(ManagerId, StaffRole.BranchManager, HomeBranch);
		manager.Deactivate();
		var service = CreateService(manager, []);

		await Assert.ThrowsAsync<UnauthorizedAccessException>(
			() => service.GetReportAsync(ManagerId, ReportDate));
	}

	[Fact]
	public async Task GetReportAsync_MissingStaffIdIsDeniedWithoutLoading()
	{
		var repository = Substitute.For<IApexPostRepository>();
		var service = new DailyBranchSalesReportApplicationService(repository);

		await Assert.ThrowsAsync<UnauthorizedAccessException>(
			() => service.GetReportAsync(" ", ReportDate));

		await repository.DidNotReceive()
			.LoadAsync(Arg.Any<CancellationToken>());
	}

	private static DailyBranchSalesReportApplicationService CreateService(
		Staff staff,
		IEnumerable<TransactionBasket> baskets)
	{
		var repository = Substitute.For<IApexPostRepository>();
		repository.LoadAsync(Arg.Any<CancellationToken>())
			.Returns(new ApexPostSnapshot(
				[],
				[staff],
				[],
				[],
				[],
				[],
				[],
				[],
				baskets));
		return new DailyBranchSalesReportApplicationService(repository);
	}

	private static Staff CreateStaff(
		string id,
		StaffRole role,
		BranchCode homeBranchCode) =>
		new(id, $"Test {id}", $"{id}@example.test", role, homeBranchCode);

	private static TransactionBasket CreatePaidBasket(
		BranchCode branchCode,
		decimal goodsTotal,
		decimal serviceTotal,
		string currency)
	{
		var taxRates = new Dictionary<TaxCategory, decimal>
		{
			[TaxCategory.Standard] = 0m
		};
		var basket = new TransactionBasket(currency, taxRates, branchCode);
		if (goodsTotal > 0)
		{
			basket.AddLineItem(new TransactionLineItem(
				"Goods",
				TransactionItemType.Goods,
				new Money(goodsTotal, currency),
				1,
				TaxCategory.Standard,
				new Sku("SKU-1")));
		}

		if (serviceTotal > 0)
		{
			basket.AddLineItem(new TransactionLineItem(
				"Service",
				TransactionItemType.Service,
				new Money(serviceTotal, currency),
				1,
				TaxCategory.Standard));
		}

		basket.Pay();
		return basket;
	}

	private static TransactionBasket CreateOpenBasket(
		BranchCode branchCode,
		string currency)
	{
		var taxRates = new Dictionary<TaxCategory, decimal>
		{
			[TaxCategory.Standard] = 0m
		};
		return new TransactionBasket(currency, taxRates, branchCode);
	}
}