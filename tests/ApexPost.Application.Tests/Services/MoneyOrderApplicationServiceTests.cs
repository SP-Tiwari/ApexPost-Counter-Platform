
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
		Assert.Null(order.IdentityVerification);
		Assert.Empty(snapshot.IdentityVerifications);
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

	private static ApexPostSnapshot CreateSnapshot() =>
		new(
			[],
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
			[],
			[],
			[]);

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
