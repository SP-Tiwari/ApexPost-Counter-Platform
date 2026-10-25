using ApexPost.Domain.Reporting;
using NSubstitute;

namespace ApexPost.Application.Tests.Services;

public sealed class BranchAuditorReportApplicationServiceTests
{
	private static readonly BranchCode HomeBranch = new("BR01");
	private static readonly BranchCode OtherBranch = new("BR02");
	private const string AuditorId = "auditor-1";

	[Fact]
	public async Task GetReportAsync_AuditorReceivesBranchTotals()
	{
		var service = CreateService(
			CreateStaff(AuditorId, StaffRole.Auditor, HomeBranch),
			[
				CreateMoneyOrder(HomeBranch, 200m, true),
				CreateMoneyOrder(HomeBranch, 50m, false),
				CreateMoneyOrder(OtherBranch, 100m, true)
			]);

		var report = await service.GetReportAsync(AuditorId);

		Assert.Equal(
			[
				new BranchAuditorReportRow(HomeBranch, 2, 2, 1, 1),
				new BranchAuditorReportRow(OtherBranch, 1, 1, 1, 0)
			],
			report);
	}

	[Fact]
	public async Task GetReportAsync_AdministratorIsAuthorized()
	{
		var service = CreateService(
			CreateStaff(AuditorId, StaffRole.Administrator, HomeBranch),
			[]);

		Assert.Empty(await service.GetReportAsync(AuditorId));
	}

	[Theory]
	[InlineData(StaffRole.BranchManager)]
	[InlineData(StaffRole.CounterClerk)]
	public async Task GetReportAsync_NonReportRolesAreDenied(StaffRole role)
	{
		var service = CreateService(
			CreateStaff(AuditorId, role, HomeBranch),
			[]);

		await Assert.ThrowsAsync<UnauthorizedAccessException>(
			() => service.GetReportAsync(AuditorId));
	}

	[Fact]
	public async Task GetReportAsync_InactiveAuditorIsDenied()
	{
		var auditor = CreateStaff(AuditorId, StaffRole.Auditor, HomeBranch);
		auditor.Deactivate();
		var service = CreateService(auditor, []);

		await Assert.ThrowsAsync<UnauthorizedAccessException>(
			() => service.GetReportAsync(AuditorId));
	}

	[Fact]
	public async Task GetReportAsync_MissingStaffIdIsDeniedWithoutLoading()
	{
		var repository = Substitute.For<IApexPostRepository>();
		var service = new BranchAuditorReportApplicationService(repository);

		await Assert.ThrowsAsync<UnauthorizedAccessException>(
			() => service.GetReportAsync(" "));

		await repository.DidNotReceive()
			.LoadAsync(Arg.Any<CancellationToken>());
	}

	private static BranchAuditorReportApplicationService CreateService(
		Staff staff,
		IEnumerable<MoneyOrder> moneyOrders)
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
				moneyOrders,
				[],
				[]));
		return new BranchAuditorReportApplicationService(repository);
	}

	private static Staff CreateStaff(
		string id,
		StaffRole role,
		BranchCode branchCode) =>
		new(id, $"Test {id}", $"{id}@example.test", role, branchCode);

	private static MoneyOrder CreateMoneyOrder(
		BranchCode branchCode,
		decimal amount,
		bool verificationSuccess)
	{
		var verification = new IdentityVerification(
			"Passport",
			$"ID-{Guid.NewGuid():N}",
			verificationSuccess);
		return new MoneyOrder(
			new Money(amount, "INR"),
			"Sender",
			"Recipient",
			new Money(5m, "INR"),
			verification,
			branchCode);
	}
}