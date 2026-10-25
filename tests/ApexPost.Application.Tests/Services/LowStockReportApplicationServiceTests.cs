using ApexPost.Domain.Reporting;
using NSubstitute;

namespace ApexPost.Application.Tests.Services;

public sealed class LowStockReportApplicationServiceTests
{
	private static readonly BranchCode HomeBranch = new("BR01");
	private static readonly BranchCode OtherBranch = new("BR02");
	private const string ManagerId = "manager-1";

	[Fact]
	public async Task GetReportAsync_ActiveManagerSeesOnlyLowStockInHomeBranch()
	{
		var repository = CreateRepository(
			CreateManager(),
			[
				new BranchStock(HomeBranch, new Sku("LOCAL-LOW"), 2),
				new BranchStock(HomeBranch, new Sku("LOCAL-OK"), 4),
				new BranchStock(OtherBranch, new Sku("FOREIGN-LOW"), 0)
			]);
		var service = new LowStockReportApplicationService(repository, 2);

		var report = await service.GetReportAsync(ManagerId);

		var row = Assert.Single(report);
		Assert.Equal(HomeBranch, row.BranchCode);
		Assert.Equal(new Sku("LOCAL-LOW"), row.ProductSku);
		Assert.Equal(2, row.Quantity);
		Assert.Equal(2, row.Threshold);
	}

	[Fact]
	public async Task GetReportAsync_NonManagerIsDenied()
	{
		var clerk = CreateStaff(ManagerId, StaffRole.CounterClerk, HomeBranch);
		var service = CreateService(clerk);

		await Assert.ThrowsAsync<UnauthorizedAccessException>(
			() => service.GetReportAsync(ManagerId));
	}

	[Fact]
	public async Task GetReportAsync_InactiveManagerIsDenied()
	{
		var manager = CreateManager();
		manager.Deactivate();
		var service = CreateService(manager);

		await Assert.ThrowsAsync<UnauthorizedAccessException>(
			() => service.GetReportAsync(ManagerId));
	}

	[Fact]
	public async Task GetReportAsync_UnknownStaffIsDenied()
	{
		var service = CreateService(CreateManager());

		await Assert.ThrowsAsync<UnauthorizedAccessException>(
			() => service.GetReportAsync("unknown-staff"));
	}

	[Fact]
	public async Task GetReportAsync_MissingStaffIdIsDenied()
	{
		var repository = Substitute.For<IApexPostRepository>();
		var service = new LowStockReportApplicationService(repository, 2);

		await Assert.ThrowsAsync<UnauthorizedAccessException>(
			() => service.GetReportAsync(" "));

		await repository.DidNotReceive()
			.LoadAsync(Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task GetReportAsync_MissingThresholdFailsAfterAuthorization()
	{
		var service = new LowStockReportApplicationService(
			CreateRepository(CreateManager(), []), null);

		await Assert.ThrowsAsync<InvalidOperationException>(
			() => service.GetReportAsync(ManagerId));
	}

	[Fact]
	public void Constructor_NegativeThresholdIsRejected()
	{
		var repository = Substitute.For<IApexPostRepository>();

		Assert.Throws<ArgumentOutOfRangeException>(
			() => new LowStockReportApplicationService(repository, -1));
	}

	private static LowStockReportApplicationService CreateService(
		Staff staff) =>
		new(CreateRepository(staff, []), 2);

	private static IApexPostRepository CreateRepository(
		Staff staff,
		IEnumerable<BranchStock> stocks)
	{
		var repository = Substitute.For<IApexPostRepository>();
		repository.LoadAsync(Arg.Any<CancellationToken>())
			.Returns(new ApexPostSnapshot(
				[],
				[staff],
				[],
				stocks,
				[],
				[],
				[],
				[],
				[]));
		return repository;
	}

	private static Staff CreateManager() =>
		CreateStaff(ManagerId, StaffRole.BranchManager, HomeBranch);

	private static Staff CreateStaff(
		string id,
		StaffRole role,
		BranchCode branchCode) =>
		new(id, $"Test {id}", $"{id}@example.test", role, branchCode);
}
