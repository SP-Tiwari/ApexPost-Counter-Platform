
namespace ApexPost.Application.Tests.Services;

public sealed class MoneyOrderApplicationServiceTests
{
	private static readonly Money Threshold = new(1000m, "INR");

	[Fact]
	public async Task CreateAndProcessAsync_LowValueOrderPersistsWithoutVerification()
	{
		var repository = new InMemoryApexPostRepository(CreateSnapshot());
		var service = new MoneyOrderApplicationService(repository, Threshold);

		await service.CreateAndProcessAsync(
			"manager-1", 500m, "Sender", "Recipient", 10m, null);

		var snapshot = await repository.LoadAsync();
		var order = Assert.Single(snapshot.MoneyOrders);
		Assert.Equal(new Money(500m, "INR"), order.Amount);
		Assert.Equal(new BranchCode("BR01"), order.ProcessingBranchCode);
		Assert.Null(order.IdentityVerification);
		Assert.Empty(snapshot.IdentityVerifications);
	}

	[Fact]
	public async Task GetMoneyOrdersAsync_ReturnsOnlyKnownOrdersForManagersHomeBranch()
	{
		var manager = CreateStaff(
			"manager-1", StaffRole.BranchManager, "BR01");
		var otherBranchOrder = new MoneyOrder(
			new Money(200m, "INR"), "Other sender", "Recipient",
			new Money(10m, "INR"), processingBranchCode: new BranchCode("BR02"));
		var ownBranchOrder = new MoneyOrder(
			new Money(300m, "INR"), "Local sender", "Recipient",
			new Money(10m, "INR"), processingBranchCode: new BranchCode("BR01"));
		var unknownBranchOrder = new MoneyOrder(
			new Money(400m, "INR"), "Legacy sender", "Recipient",
			new Money(10m, "INR"));
		var repository = new InMemoryApexPostRepository(
			CreateSnapshot(
				[manager, CreateStaff(
					"manager-2", StaffRole.BranchManager, "BR02")],
				[ownBranchOrder, otherBranchOrder, unknownBranchOrder]));
		var service = new MoneyOrderApplicationService(repository, Threshold);

		var orders = await service.GetMoneyOrdersAsync("manager-1");

		Assert.Equal([ownBranchOrder], orders);
	}

	[Fact]
	public async Task GetMoneyOrdersAsync_RejectsInactiveStaff()
	{
		var inactiveManager = CreateStaff(
			"manager-1", StaffRole.BranchManager, "BR01");
		inactiveManager.Deactivate();
		var service = new MoneyOrderApplicationService(
			new InMemoryApexPostRepository(CreateSnapshot([inactiveManager])),
			Threshold);

		await Assert.ThrowsAsync<UnauthorizedAccessException>(
			() => service.GetMoneyOrdersAsync("manager-1"));
	}

	[Fact]
	public async Task GetMoneyOrdersAsync_RejectsNonManagerStaff()
	{
		var service = new MoneyOrderApplicationService(
			new InMemoryApexPostRepository(CreateSnapshot()),
			Threshold);

		await Assert.ThrowsAsync<UnauthorizedAccessException>(
			() => service.GetMoneyOrdersAsync("clerk-1"));
	}

	[Fact]
	public async Task CreateAndProcessAsync_HighValueSuccessfulCheckPersistsMaskedVerificationWithOrder()
	{
		var repository = new InMemoryApexPostRepository(CreateSnapshot());
		var service = new MoneyOrderApplicationService(repository, Threshold);
		var verification = new IdentityVerification(
			"Passport", "ABCD1234", isSuccessful: true);

		await service.CreateAndProcessAsync(
			"manager-1", 1000.01m, "Sender", "Recipient", 10m,
			verification);

		var snapshot = await repository.LoadAsync();
		var order = Assert.Single(snapshot.MoneyOrders);
		var orderVerification = Assert.IsType<IdentityVerification>(
			order.IdentityVerification);
		Assert.Same(verification, orderVerification);
		Assert.Equal("****1234", orderVerification.MaskedDocumentIdentifier);
		Assert.Equal(verification, Assert.Single(snapshot.IdentityVerifications));
		Assert.Equal(new BranchCode("BR01"), order.ProcessingBranchCode);
	}

	[Fact]
	public async Task CreateAndProcessAsync_HighValueWithoutVerificationRejectsWithoutPersisting()
	{
		var repository = new InMemoryApexPostRepository(CreateSnapshot());
		var service = new MoneyOrderApplicationService(repository, Threshold);

		await Assert.ThrowsAsync<InvalidOperationException>(() =>
			service.CreateAndProcessAsync(
				"manager-1", 1000.01m, "Sender", "Recipient", 10m, null));

		var snapshot = await repository.LoadAsync();
		Assert.Empty(snapshot.MoneyOrders);
		Assert.Empty(snapshot.IdentityVerifications);
	}

	[Fact]
	public async Task CreateAndProcessAsync_FailedVerificationRejectsEvenLowValueOrder()
	{
		var repository = new InMemoryApexPostRepository(CreateSnapshot());
		var service = new MoneyOrderApplicationService(repository, Threshold);
		var verification = new IdentityVerification(
			"Passport", "ABCD1234", isSuccessful: false);

		await Assert.ThrowsAsync<InvalidOperationException>(() =>
			service.CreateAndProcessAsync(
				"manager-1", 500m, "Sender", "Recipient", 10m,
				verification));

		Assert.Empty((await repository.LoadAsync()).MoneyOrders);
	}

	[Fact]
	public async Task CreateAndProcessAsync_RequiresActiveBranchManager()
	{
		var repository = new InMemoryApexPostRepository(CreateSnapshot());
		var service = new MoneyOrderApplicationService(repository, Threshold);

		await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
			service.CreateAndProcessAsync(
				"clerk-1", 500m, "Sender", "Recipient", 10m, null));

		Assert.Empty((await repository.LoadAsync()).MoneyOrders);
	}

	[Fact]
	public async Task CreateAndProcessAsync_RejectsInactiveBranchManager()
	{
		var inactiveManager = CreateStaff(
			"manager-1", StaffRole.BranchManager, "BR01");
		inactiveManager.Deactivate();
		var repository = new InMemoryApexPostRepository(
			CreateSnapshot([inactiveManager]));
		var service = new MoneyOrderApplicationService(repository, Threshold);

		await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
			service.CreateAndProcessAsync(
				"manager-1", 500m, "Sender", "Recipient", 10m, null));

		Assert.Empty((await repository.LoadAsync()).MoneyOrders);
	}

	[Fact]
	public async Task CreateAndProcessAsync_InvalidAmountDoesNotPersist()
	{
		var repository = new InMemoryApexPostRepository(CreateSnapshot());
		var service = new MoneyOrderApplicationService(repository, Threshold);

		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
			service.CreateAndProcessAsync(
				"manager-1", 0m, "Sender", "Recipient", 10m, null));

		Assert.Empty((await repository.LoadAsync()).MoneyOrders);
	}

	[Fact]
	public async Task CreateAndProcessAsync_WithoutThresholdFailsClosed()
	{
		var repository = new InMemoryApexPostRepository(CreateSnapshot());
		var service = new MoneyOrderApplicationService(repository, null);

		await Assert.ThrowsAsync<InvalidOperationException>(() =>
			service.CreateAndProcessAsync(
				"manager-1", 500m, "Sender", "Recipient", 10m, null));

		Assert.Empty((await repository.LoadAsync()).MoneyOrders);
	}

	private static ApexPostSnapshot CreateSnapshot(
		IEnumerable<Staff>? staff = null,
		IEnumerable<MoneyOrder>? moneyOrders = null) =>
		new(
			[],
			staff ??
			[
				new Staff(
					"manager-1",
					"Manager",
					"manager@example.test",
					StaffRole.BranchManager,
					new BranchCode("BR01")),
				new Staff(
					"clerk-1",
					"Clerk",
					"clerk@example.test",
					StaffRole.CounterClerk,
					new BranchCode("BR01"))
			],
			[],
			[],
			[],
			[],
			moneyOrders ?? [],
			[],
			[]);

	private static Staff CreateStaff(
		string id,
		StaffRole role,
		string branchCode) =>
		new(
			id,
			id,
			$"{id}@example.test",
			role,
			new BranchCode(branchCode));

	private sealed class InMemoryApexPostRepository(
		ApexPostSnapshot initialSnapshot) : IApexPostRepository
	{
		private ApexPostSnapshot _snapshot = initialSnapshot;

		public Task<ApexPostSnapshot> LoadAsync(
			CancellationToken cancellationToken = default)
		{
			cancellationToken.ThrowIfCancellationRequested();
			return Task.FromResult(_snapshot);
		}

		public Task SaveAsync(
			ApexPostSnapshot snapshot,
			CancellationToken cancellationToken = default)
		{
			cancellationToken.ThrowIfCancellationRequested();
			_snapshot = snapshot;
			return Task.CompletedTask;
		}

		public Task UpdateAsync(
			Func<ApexPostSnapshot, ApexPostSnapshot> update,
			CancellationToken cancellationToken = default)
		{
			cancellationToken.ThrowIfCancellationRequested();
			_snapshot = update(_snapshot);
			return Task.CompletedTask;
		}
	}
}
