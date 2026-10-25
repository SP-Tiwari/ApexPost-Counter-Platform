using ApexPost.Application.Persistence;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;
using ApexPost.Infrastructure.Persistence;

namespace ApexPost.Infrastructure.Tests.Persistence;

public sealed class DemoDataInitializerTests : IAsyncLifetime
{
	private string _storageDirectory = null!;

	public Task InitializeAsync()
	{
		_storageDirectory = Path.Combine(
			Path.GetTempPath(),
			$"ApexPost-DemoData-{Guid.NewGuid():N}");
		Directory.CreateDirectory(_storageDirectory);
		return Task.CompletedTask;
	}

	public Task DisposeAsync()
	{
		if (Directory.Exists(_storageDirectory))
			Directory.Delete(_storageDirectory, recursive: true);
		return Task.CompletedTask;
	}

	[Fact]
	public async Task InitializeAsync_SeedsExpectedFictionalDataOnlyOnce()
	{
		using var repository = CreateRepository();
		var initializer = new DemoDataInitializer(repository);

		Assert.True(await initializer.InitializeAsync());
		Assert.False(await initializer.InitializeAsync());

		var snapshot = await repository.LoadAsync();
		Assert.Equal(5, snapshot.Branches.Count);
		Assert.Equal(20, snapshot.Staff.Count);
		Assert.Equal(30, snapshot.Products.Count);
		Assert.Equal(10, snapshot.ShippingRates.Count);
		Assert.Equal(40, snapshot.TransactionBaskets.Count);
		var branchCodes = snapshot.Branches
			.Select(branch => branch.Code)
			.ToHashSet();
		Assert.All(snapshot.Staff, staff =>
		{
			Assert.StartsWith("DEMO ", staff.Name, StringComparison.Ordinal);
			Assert.EndsWith("@example.invalid", staff.Email, StringComparison.Ordinal);
			Assert.Contains(staff.HomeBranchCode, branchCodes);
		});
		Assert.Contains(StaffRole.CounterClerk, snapshot.Staff.Select(staff => staff.Role));
		Assert.Contains(StaffRole.BranchManager, snapshot.Staff.Select(staff => staff.Role));
		Assert.Contains(StaffRole.Auditor, snapshot.Staff.Select(staff => staff.Role));
		Assert.Contains(StaffRole.Administrator, snapshot.Staff.Select(staff => staff.Role));
		Assert.All(snapshot.TransactionBaskets, basket =>
			Assert.Equal(
				ApexPost.Domain.Enums.TransactionPaymentStatus.Paid,
				basket.PaymentStatus));
		Assert.All(snapshot.ShippingRates, rate =>
			Assert.StartsWith("DEMO-ZONE-", rate.DestinationZone, StringComparison.Ordinal));
		Assert.False(File.Exists(
			Path.Combine(_storageDirectory, "staff-credentials.json")));
	}

	[Fact]
	public async Task InitializeAsync_DoesNotModifyANonEmptySnapshot()
	{
		using var repository = CreateRepository();
		var branch = new Branch(
			new BranchCode("EX01"),
			"Existing branch",
			"Existing region",
			"Configured hours",
			[new CounterTerminal("EX01-T1", "Counter 1")]);
		await repository.SaveAsync(new ApexPostSnapshot(
			[branch], [], [], [], [], [], [], [], []));

		var initialized = await new DemoDataInitializer(repository)
			.InitializeAsync();

		Assert.False(initialized);
		var snapshot = await repository.LoadAsync();
		Assert.Equal("Existing branch", Assert.Single(snapshot.Branches).Name);
		Assert.Empty(snapshot.Staff);
		Assert.Empty(snapshot.Products);
	}

	private JsonApexPostRepository CreateRepository() =>
		new(_storageDirectory);
}
