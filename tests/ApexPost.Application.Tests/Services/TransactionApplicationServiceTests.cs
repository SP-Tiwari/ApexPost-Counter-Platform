using ApexPost.Application.Persistence;
using ApexPost.Application.Services;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.Notifications;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Application.Tests.Services;

public sealed class TransactionApplicationServiceTests
{
	private static readonly BranchCode BranchOne = new("BR01");
	private static readonly BranchCode BranchTwo = new("BR02");
	private static readonly Sku ProductSku = new("BOX-01");

	[Fact]
	public async Task CreateBasketAndAddLineItemAsync_PersistsBasketWithExplicitTaxRates()
	{
		var repository = new InMemoryApexPostRepository(CreateSnapshot());
		var service = new TransactionApplicationService(repository);
		var rates = new Dictionary<TaxCategory, decimal>
		{
			[TaxCategory.Standard] = 0.1m
		};

		var transactionId = await service.CreateBasketAsync("INR", rates);
		await service.AddLineItemAsync(
			transactionId,
			new TransactionLineItem(
				"Counter service",
				TransactionItemType.Service,
				new Money(10m, "INR"),
				1,
				TaxCategory.Standard));

		var basket = Assert.Single((await repository.LoadAsync()).TransactionBaskets);
		Assert.Equal(transactionId, basket.Id);
		Assert.Equal(TransactionPaymentStatus.Open, basket.PaymentStatus);
		Assert.Equal(0.1m, basket.TaxRates[TaxCategory.Standard]);
		Assert.Equal(new Money(11m, "INR"), basket.Total);
		Assert.Null(Assert.Single(basket.LineItems).ProductSku);
	}

	[Fact]
	public async Task ProcessSaleAsync_UsesDomainRulesAndPersistsStockDeduction()
	{
		var repository = new InMemoryApexPostRepository(CreateSnapshot());
		var service = new TransactionApplicationService(repository);
		var transactionId = await CreateGoodsBasketAsync(service);

		await service.ProcessSaleAsync(transactionId, "manager-1", BranchOne);

		var snapshot = await repository.LoadAsync();
		Assert.Equal(
			TransactionPaymentStatus.Paid,
			Assert.Single(snapshot.TransactionBaskets).PaymentStatus);
		Assert.Equal(2, Assert.Single(snapshot.BranchStocks).Quantity);
	}

	[Fact]
	public async Task ProcessSaleAsync_WhenStaffIsNotAssignedToBranch_DoesNotPersistMutation()
	{
		var repository = new InMemoryApexPostRepository(CreateSnapshot());
		var service = new TransactionApplicationService(repository);
		var transactionId = await CreateGoodsBasketAsync(service);

		await Assert.ThrowsAsync<InvalidOperationException>(() =>
			service.ProcessSaleAsync(transactionId, "clerk-2", BranchOne));

		var snapshot = await repository.LoadAsync();
		Assert.Equal(
			TransactionPaymentStatus.Open,
			Assert.Single(snapshot.TransactionBaskets).PaymentStatus);
		Assert.Equal(4, Assert.Single(snapshot.BranchStocks).Quantity);
	}

	[Fact]
	public async Task VoidAndRefundAsync_RequireManagerAndPersistReasons()
	{
		var repository = new InMemoryApexPostRepository(CreateSnapshot());
		var service = new TransactionApplicationService(repository);
		var voidId = await service.CreateBasketAsync("INR", CreateTaxRates());
		var refundId = await CreateGoodsBasketAsync(service);
		await service.ProcessSaleAsync(refundId, "manager-1", BranchOne);

		await service.VoidAsync(voidId, "manager-1", "Duplicate entry");
		await service.RefundAsync(refundId, "manager-1", "Customer request");

		var baskets = (await repository.LoadAsync()).TransactionBaskets;
		var voided = Assert.Single(baskets, item => item.Id == voidId);
		var refunded = Assert.Single(baskets, item => item.Id == refundId);
		Assert.Equal(TransactionPaymentStatus.Voided, voided.PaymentStatus);
		Assert.Equal(
			"Duplicate entry",
			Assert.IsType<TransactionVoidedNotification>(
				Assert.Single(voided.Notifications)).Reason);
		Assert.Equal(TransactionPaymentStatus.Refunded, refunded.PaymentStatus);
		Assert.Equal(
			"Customer request",
			Assert.IsType<TransactionRefundedNotification>(
				Assert.Single(refunded.Notifications)).Reason);

		await Assert.ThrowsAsync<InvalidOperationException>(() =>
			service.VoidAsync(voidId, "clerk-1", "Unauthorized"));
		Assert.Equal(
			TransactionPaymentStatus.Voided,
			Assert.Single(
				(await repository.LoadAsync()).TransactionBaskets,
				item => item.Id == voidId).PaymentStatus);
	}

	[Fact]
	public async Task OperationsAsync_WhenTransactionDoesNotExist_ReportsMissingInput()
	{
		var service = new TransactionApplicationService(
			new InMemoryApexPostRepository(CreateSnapshot()));

		await Assert.ThrowsAsync<KeyNotFoundException>(() =>
			service.AddLineItemAsync(
				Guid.NewGuid(),
				new TransactionLineItem(
					"Service",
					TransactionItemType.Service,
					new Money(1m, "INR"),
					1,
					TaxCategory.Standard)));
	}

	[Fact]
	public async Task CreateBasketAsync_PropagatesCancellation()
	{
		var repository = new InMemoryApexPostRepository(CreateSnapshot());
		var service = new TransactionApplicationService(repository);
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
			service.CreateBasketAsync(
				"INR", CreateTaxRates(), cancellation.Token));
		Assert.Empty((await repository.LoadAsync()).TransactionBaskets);
	}

	private static async Task<Guid> CreateGoodsBasketAsync(
		TransactionApplicationService service)
	{
		var transactionId = await service.CreateBasketAsync(
			"INR", CreateTaxRates());
		await service.AddLineItemAsync(
			transactionId,
			new TransactionLineItem(
				"Box",
				TransactionItemType.Goods,
				new Money(20m, "INR"),
				2,
				TaxCategory.Standard,
				ProductSku));
		return transactionId;
	}

	private static Dictionary<TaxCategory, decimal> CreateTaxRates() => new()
	{
		[TaxCategory.Standard] = 0m
	};

	private static ApexPostSnapshot CreateSnapshot()
	{
		var branchOne = CreateBranch(BranchOne);
		var branchTwo = CreateBranch(BranchTwo);
		return new ApexPostSnapshot(
			[branchOne, branchTwo],
			[
				CreateStaff("manager-1", StaffRole.BranchManager, BranchOne),
				CreateStaff("clerk-1", StaffRole.CounterClerk, BranchOne),
				CreateStaff("clerk-2", StaffRole.CounterClerk, BranchTwo)
			],
			[
				new Product(
					ProductSku,
					"Box",
					ProductCategory.Packaging,
					new Money(20m, "INR"),
					TaxCategory.Standard)
			],
			[new BranchStock(BranchOne, ProductSku, 4)],
			[],
			[],
			[],
			[],
			[]);
	}

	private static Branch CreateBranch(BranchCode code) =>
		new(
			code,
			$"Branch {code}",
			"Region",
			"09:00-17:00",
			[new CounterTerminal($"COUNTER-{code}", "Counter")]);

	private static Staff CreateStaff(
		string id,
		StaffRole role,
		BranchCode branchCode) =>
		new(id, id, $"{id}@example.test", role, branchCode);

	private sealed class InMemoryApexPostRepository : IApexPostRepository
	{
		private readonly SemaphoreSlim _gate = new(1, 1);
		private ApexPostSnapshot _snapshot;

		public InMemoryApexPostRepository(ApexPostSnapshot snapshot) =>
			_snapshot = Clone(snapshot);

		public async Task<ApexPostSnapshot> LoadAsync(
			CancellationToken cancellationToken = default)
		{
			await _gate.WaitAsync(cancellationToken);
			try
			{
				return Clone(_snapshot);
			}
			finally
			{
				_gate.Release();
			}
		}

		public async Task SaveAsync(
			ApexPostSnapshot snapshot,
			CancellationToken cancellationToken = default)
		{
			await _gate.WaitAsync(cancellationToken);
			try
			{
				cancellationToken.ThrowIfCancellationRequested();
				_snapshot = Clone(snapshot);
			}
			finally
			{
				_gate.Release();
			}
		}

		public async Task UpdateAsync(
			Func<ApexPostSnapshot, ApexPostSnapshot> update,
			CancellationToken cancellationToken = default)
		{
			await _gate.WaitAsync(cancellationToken);
			try
			{
				var updated = update(Clone(_snapshot));
				cancellationToken.ThrowIfCancellationRequested();
				_snapshot = Clone(updated);
			}
			finally
			{
				_gate.Release();
			}
		}

		private static ApexPostSnapshot Clone(ApexPostSnapshot snapshot)
		{
			var branches = snapshot.Branches.Select(branch =>
			{
				var terminals = branch.CounterTerminals.Select(terminal =>
				{
					var copy = new CounterTerminal(terminal.Id, terminal.Name);
					if (!terminal.IsActive)
						copy.Deactivate();
					return copy;
				});
				var copy = new Branch(
					branch.Code,
					branch.Name,
					branch.Region,
					branch.OpeningHours,
					terminals);
				if (!branch.IsActive)
					copy.Close();
				return copy;
			});
			var staff = snapshot.Staff.Select(person =>
			{
				var copy = new Staff(
					person.Id,
					person.Name,
					person.Email,
					person.Role,
					person.HomeBranchCode);
				if (!person.IsActive)
					copy.Deactivate();
				return copy;
			});
			var stocks = snapshot.BranchStocks.Select(stock =>
				new BranchStock(
					stock.BranchCode, stock.ProductSku, stock.Quantity));
			var baskets = snapshot.TransactionBaskets.Select(basket =>
			{
				var state = basket.GetSnapshot();
				return TransactionBasket.Reconstitute(
					state.Id,
					state.Currency,
					state.TaxRates,
					state.LineItems,
					state.PaymentStatus,
					state.Notifications);
			});

			return new ApexPostSnapshot(
				branches,
				staff,
				snapshot.Products,
				stocks,
				snapshot.ShippingItems,
				snapshot.ShippingRates,
				snapshot.MoneyOrders,
				snapshot.IdentityVerifications,
				baskets);
		}
	}
}
