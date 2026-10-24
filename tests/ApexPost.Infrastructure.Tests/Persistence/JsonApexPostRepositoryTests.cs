using System.Text.Json;
using ApexPost.Application.Persistence;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.Notifications;
using ApexPost.Domain.Rules;
using ApexPost.Domain.ValueObjects;
using ApexPost.Infrastructure.Persistence;

namespace ApexPost.Infrastructure.Tests.Persistence;

public sealed class JsonApexPostRepositoryTests : IAsyncLifetime
{
	private string _storageDirectory = null!;
	private const string FullDocumentIdentifier = "SECRET-DOCUMENT-1234";

	public Task InitializeAsync()
	{
		_storageDirectory = Path.Combine(
			Path.GetTempPath(),
			$"ApexPost-{Guid.NewGuid():N}");
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
	public async Task SaveAndLoadAsync_RoundTripsDomainDataAndPreservesInvariants()
	{
		using var repository = CreateRepository();
		var branch = CreateBranch();
		branch.Close();
		branch.CounterTerminals[0].Deactivate();

		var staff = CreateStaff();
		staff.Deactivate();

		var product = CreateProduct();
		var basket = CreateBasket(product);
		basket.AddLineItem(new TransactionLineItem(
			"Parcel service",
			TransactionItemType.Service,
			new Money(5m, "INR"),
			1,
			TaxCategory.Standard));
		basket.Pay();
		new TransactionVoidRefundRule().Refund(
			CreateManager(),
			basket,
			"Customer request");
		var verification = new IdentityVerification(
			"Passport",
			FullDocumentIdentifier,
			true);
		var paidBasket = CreateBasket(product);
		paidBasket.Pay();
		var voidedBasket = CreateBasket(product);
		new TransactionVoidRefundRule().Void(
			CreateManager(),
			voidedBasket,
			"Duplicate entry");
		var openBasket = new TransactionBasket(
			"INR",
			new Dictionary<TaxCategory, decimal>
			{
				[TaxCategory.Standard] = 0.1m
			});
		var snapshot = new ApexPostSnapshot(
			[branch],
			[staff],
			[product],
			[new BranchStock(new BranchCode("DEL01"), product.Sku, 8)],
			[CreateShippingItem()],
			[CreateShippingRate()],
			[CreateMoneyOrder()],
			[verification],
			[openBasket, paidBasket, voidedBasket, basket]);

		await repository.SaveAsync(snapshot);
		var loaded = await repository.LoadAsync();
		var persistedJson = await File.ReadAllTextAsync(DataFilePath);

		var loadedBranch = Assert.Single(loaded.Branches);
		Assert.False(loadedBranch.IsActive);
		Assert.False(Assert.Single(loadedBranch.CounterTerminals).IsActive);
		Assert.False(Assert.Single(loaded.Staff).IsActive);
		var loadedProduct = Assert.Single(loaded.Products);
		Assert.Equal(product.Sku, loadedProduct.Sku);
		Assert.Equal(product.UnitPrice, loadedProduct.UnitPrice);
		Assert.Equal(8, Assert.Single(loaded.BranchStocks).Quantity);
		var loadedShippingItem = Assert.Single(loaded.ShippingItems);
		Assert.Equal("SHIP01", loadedShippingItem.Id);
		Assert.Equal(ShippingItemType.Parcel, loadedShippingItem.Type);
		Assert.Equal(new Weight(2m), loadedShippingItem.Weight);
		Assert.Equal(new Dimensions(10m, 20m, 30m), loadedShippingItem.Dimensions);
		Assert.Equal("ZONE-A", loadedShippingItem.DestinationZone);
		Assert.Equal(ShippingServiceLevel.Standard, loadedShippingItem.ServiceLevel);
		Assert.Equal(CreateShippingRate(), Assert.Single(loaded.ShippingRates));
		var loadedMoneyOrder = Assert.Single(loaded.MoneyOrders);
		Assert.Equal(new Money(100m, "INR"), loadedMoneyOrder.Amount);
		Assert.Equal("Sender", loadedMoneyOrder.Sender);
		Assert.Equal("Recipient", loadedMoneyOrder.Recipient);
		Assert.Equal(new Money(5m, "INR"), loadedMoneyOrder.Fee);

		var loadedVerification = Assert.Single(loaded.IdentityVerifications);
		Assert.Equal("Passport", loadedVerification.DocumentType);
		Assert.True(loadedVerification.IsSuccessful);
		Assert.EndsWith("1234", loadedVerification.MaskedDocumentIdentifier);
		Assert.All(
			loadedVerification.MaskedDocumentIdentifier[..^4],
			character => Assert.Equal('*', character));
		Assert.DoesNotContain(
			FullDocumentIdentifier,
			persistedJson,
			StringComparison.Ordinal);

		var loadedBasket = loaded.TransactionBaskets[0];
		Assert.Equal(TransactionPaymentStatus.Open, loadedBasket.PaymentStatus);
		Assert.Equal(
			TransactionPaymentStatus.Paid,
			loaded.TransactionBaskets[1].PaymentStatus);
		Assert.Equal(
			TransactionPaymentStatus.Voided,
			loaded.TransactionBaskets[2].PaymentStatus);
		var loadedRefundedBasket = loaded.TransactionBaskets[3];
		Assert.Equal(TransactionPaymentStatus.Refunded, loadedRefundedBasket.PaymentStatus);
		Assert.Equal(new Money(25m, "INR"), loadedRefundedBasket.Subtotal);
		Assert.Equal(new Money(2.5m, "INR"), loadedRefundedBasket.Tax);
		Assert.Equal(new Money(27.5m, "INR"), loadedRefundedBasket.Total);
		Assert.Equal(0.1m, loadedRefundedBasket.TaxRates[TaxCategory.Standard]);
		Assert.Equal(2, loadedRefundedBasket.LineItems.Count);
		Assert.Equal(product.Sku, loadedRefundedBasket.LineItems[0].ProductSku);
		var loadedServiceLine = loadedRefundedBasket.LineItems[1];
		Assert.Equal(TransactionItemType.Service, loadedServiceLine.Type);
		Assert.Null(loadedServiceLine.ProductSku);
		Assert.Equal("Parcel service", loadedServiceLine.Description);
		Assert.Equal(new Money(5m, "INR"), loadedServiceLine.UnitPrice);
		Assert.Equal(1, loadedServiceLine.Quantity);
		Assert.Equal(
			"Customer request",
			Assert.IsType<TransactionRefundedNotification>(
				Assert.Single(loadedRefundedBasket.Notifications)).Reason);
		Assert.Equal(
			"Duplicate entry",
			Assert.IsType<TransactionVoidedNotification>(
				Assert.Single(loaded.TransactionBaskets[2].Notifications)).Reason);
	}

	[Fact]
	public async Task LoadAsync_WhenFileIsMissing_ReturnsEmptySnapshot()
	{
		using var repository = CreateRepository();
		var snapshot = await repository.LoadAsync();

		Assert.Empty(snapshot.Branches);
		Assert.Empty(snapshot.TransactionBaskets);
		Assert.False(File.Exists(DataFilePath));
	}

	[Fact]
	public async Task LoadAsync_WhenFilePathIsUnreadable_PropagatesOpenError()
	{
		Directory.CreateDirectory(DataFilePath);
		await File.WriteAllTextAsync(
			Path.Combine(DataFilePath, "sentinel.txt"),
			"preserve");
		using var repository = CreateRepository();

		var exception = await Record.ExceptionAsync(
			() => repository.LoadAsync());

		Assert.True(
			exception is IOException or UnauthorizedAccessException,
			$"Expected a file-open error but received {exception?.GetType().Name ?? "no exception"}.");
		Assert.Equal(
			"preserve",
			await File.ReadAllTextAsync(Path.Combine(DataFilePath, "sentinel.txt")));
	}

	[Fact]
	public async Task UpdateAsync_WhenFileCannotBeOpened_DoesNotTransformOrReplaceData()
	{
		Directory.CreateDirectory(DataFilePath);
		var sentinelPath = Path.Combine(DataFilePath, "sentinel.txt");
		await File.WriteAllTextAsync(sentinelPath, "preserve");
		using var repository = CreateRepository();
		var updateInvoked = false;

		var exception = await Record.ExceptionAsync(() =>
			repository.UpdateAsync(snapshot =>
			{
				updateInvoked = true;
				return snapshot;
			}));

		Assert.True(
			exception is IOException or UnauthorizedAccessException,
			$"Expected a file-open error but received {exception?.GetType().Name ?? "no exception"}.");
		Assert.False(updateInvoked);
		Assert.Equal("preserve", await File.ReadAllTextAsync(sentinelPath));
	}

	[Theory]
	[InlineData("")]
	[InlineData("{ \"schemaVersion\":")]
	public async Task LoadAsync_WhenFileIsEmptyOrMalformed_ThrowsJsonException(
		string contents)
	{
		await File.WriteAllTextAsync(DataFilePath, contents);

		using var repository = CreateRepository();
		await Assert.ThrowsAsync<JsonException>(
			() => repository.LoadAsync());
	}

	[Fact]
	public async Task LoadAsync_WhenPersistedDomainDataIsInvalid_ThrowsInvalidDataException()
	{
		using var repository = CreateRepository();
		await repository.SaveAsync(CreateSnapshot(
			branchStocks:
			[
				new BranchStock(new BranchCode("DEL01"), new Sku("BOX01"), 3)
			]));
		var json = await File.ReadAllTextAsync(DataFilePath);
		await File.WriteAllTextAsync(
			DataFilePath,
			json.Replace("\"quantity\":3", "\"quantity\":-1", StringComparison.Ordinal));

		await Assert.ThrowsAsync<InvalidDataException>(
			() => repository.LoadAsync());
	}

	[Fact]
	public async Task LoadAsync_WhenPersistedCollectionContainsNull_ThrowsInvalidDataException()
	{
		using var repository = CreateRepository();
		await repository.SaveAsync(ApexPostSnapshot.Empty);
		var json = await File.ReadAllTextAsync(DataFilePath);
		await File.WriteAllTextAsync(
			DataFilePath,
			json.Replace("\"branches\":[]", "\"branches\":[null]", StringComparison.Ordinal));

		await Assert.ThrowsAsync<InvalidDataException>(
			() => repository.LoadAsync());
	}

	[Fact]
	public async Task LoadAsync_WhenRequiredStateFieldIsMissing_ThrowsJsonException()
	{
		using var repository = CreateRepository();
		await repository.SaveAsync(new ApexPostSnapshot(
			[CreateBranch()],
			[],
			[],
			[],
			[],
			[],
			[],
			[],
			[]));
		var json = await File.ReadAllTextAsync(DataFilePath);
		await File.WriteAllTextAsync(
			DataFilePath,
			json.Replace("\"isActive\":true,", "", StringComparison.Ordinal));

		await Assert.ThrowsAsync<JsonException>(
			() => repository.LoadAsync());
	}

	[Fact]
	public async Task LoadAndSaveAsync_WhenCanceled_ThrowAndDoNotCreateOrReplaceData()
	{
		using var repository = CreateRepository();
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => repository.LoadAsync(cancellation.Token));
		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => repository.SaveAsync(CreateSnapshot(), cancellation.Token));
		Assert.False(File.Exists(DataFilePath));
	}

	[Fact]
	public async Task UpdateAsync_WhenCanceledAfterLoad_DoesNotReplaceExistingData()
	{
		using var repository = CreateRepository();
		await repository.SaveAsync(CreateSnapshot(products: [CreateProduct()]));
		var originalContents = await File.ReadAllTextAsync(DataFilePath);
		using var cancellation = new CancellationTokenSource();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
			repository.UpdateAsync(
				snapshot =>
				{
					cancellation.Cancel();
					return snapshot;
				},
				cancellation.Token));

		Assert.Equal(originalContents, await File.ReadAllTextAsync(DataFilePath));
		Assert.Empty(Directory.GetFiles(_storageDirectory, "*.tmp"));
		var loaded = await repository.LoadAsync();
		Assert.Equal("BOX01", Assert.Single(loaded.Products).Sku.Value);
	}

	[Fact]
	public async Task ConcurrentReadsAndWritesAsync_KeepTheJsonDocumentReadable()
	{
		using var repository = CreateRepository();
		var snapshots = Enumerable.Range(0, 16)
			.Select(index => CreateSnapshot(
				products:
				[
					new Product(
						new Sku($"BOX{index:00}"),
						$"Box {index}",
						ProductCategory.Packaging,
						new Money(1m, "INR"),
						TaxCategory.Standard)
				]))
			.ToArray();

		var operations = snapshots.SelectMany(snapshot => new Func<Task>[]
		{
			() => repository.SaveAsync(snapshot),
			async () => _ = await repository.LoadAsync()
		});

		await Task.WhenAll(operations.Select(operation => operation()));
		var loaded = await repository.LoadAsync();

		Assert.Single(loaded.Products);
		Assert.Matches("BOX[0-9]{2}", Assert.Single(loaded.Products).Sku.Value);
	}

	[Fact]
	public async Task ConcurrentUpdatesAsync_DoNotLoseReadModifyWriteChanges()
	{
		using var repository = CreateRepository();
		var updates = Enumerable.Range(0, 12).Select(index =>
			repository.UpdateAsync(snapshot => new ApexPostSnapshot(
				snapshot.Branches,
				snapshot.Staff,
				snapshot.Products.Append(new Product(
					new Sku($"SKU{index:00}"),
					$"Product {index}",
					ProductCategory.Stationery,
					new Money(1m, "INR"),
					TaxCategory.Standard)),
				snapshot.BranchStocks,
				snapshot.ShippingItems,
				snapshot.ShippingRates,
				snapshot.MoneyOrders,
				snapshot.IdentityVerifications,
				snapshot.TransactionBaskets)));

		await Task.WhenAll(updates);

		var loaded = await repository.LoadAsync();
		Assert.Equal(12, loaded.Products.Count);
		Assert.Equal(
			12,
			loaded.Products.Select(product => product.Sku).Distinct().Count());
	}

	[Fact]
	public async Task UpdateAsync_WhenTransformationFails_LeavesExistingFileUnchanged()
	{
		using var repository = CreateRepository();
		await repository.SaveAsync(CreateSnapshot(
			products: [CreateProduct()]));
		var originalContents = await File.ReadAllTextAsync(DataFilePath);

		await Assert.ThrowsAsync<InvalidOperationException>(() =>
			repository.UpdateAsync(_ =>
				throw new InvalidOperationException("Update rejected.")));

		Assert.Equal(originalContents, await File.ReadAllTextAsync(DataFilePath));
	}

	[Fact]
	public async Task SaveAsync_WhenAtomicReplacementFails_PreservesExistingFile()
	{
		using var repository = CreateRepository();
		await repository.SaveAsync(CreateSnapshot());
		var originalContents = await File.ReadAllTextAsync(DataFilePath);

		using (new FileStream(
			DataFilePath,
			FileMode.Open,
			FileAccess.Read,
			FileShare.None))
		{
			var exception = await Record.ExceptionAsync(
				() => repository.SaveAsync(CreateSnapshot(
					products: [CreateProduct()])));
			Assert.True(
				exception is IOException or UnauthorizedAccessException,
				$"Expected a file replacement error but received {exception?.GetType().Name ?? "no exception"}.");
		}

		Assert.Equal(originalContents, await File.ReadAllTextAsync(DataFilePath));
		Assert.Empty(Directory.GetFiles(_storageDirectory, "*.tmp"));
	}

	private string DataFilePath => Path.Combine(_storageDirectory, "apexpost.json");

	private JsonApexPostRepository CreateRepository() =>
		new(_storageDirectory);

	private static ApexPostSnapshot CreateSnapshot(
		IEnumerable<BranchStock>? branchStocks = null,
		IEnumerable<Product>? products = null) =>
		new(
			[],
			[],
			products ?? [],
			branchStocks ?? [],
			[],
			[],
			[],
			[],
			[]);

	private static Branch CreateBranch() =>
		new(
			new BranchCode("DEL01"),
			"Delhi Branch",
			"North",
			"09:00-17:00",
			[new CounterTerminal("COUNTER01", "Counter 1")]);

	private static Staff CreateStaff() =>
		new(
			"STAFF01",
			"Staff Member",
			"staff@example.com",
			StaffRole.CounterClerk,
			new BranchCode("DEL01"));

	private static Staff CreateManager() =>
		new(
			"MANAGER01",
			"Branch Manager",
			"manager@example.com",
			StaffRole.BranchManager,
			new BranchCode("DEL01"));

	private static Product CreateProduct() =>
		new(
			new Sku("BOX01"),
			"Cardboard Box",
			ProductCategory.Packaging,
			new Money(10m, "INR"),
			TaxCategory.Standard);

	private static TransactionBasket CreateBasket(Product product)
	{
		var basket = new TransactionBasket(
			"INR",
			new Dictionary<TaxCategory, decimal>
			{
				[TaxCategory.Standard] = 0.1m
			});
		basket.AddLineItem(new TransactionLineItem(product, 2));
		return basket;
	}

	private static ShippingItem CreateShippingItem() =>
		new(
			"SHIP01",
			ShippingItemType.Parcel,
			new Weight(2m),
			new Dimensions(10m, 20m, 30m),
			"ZONE-A",
			ShippingServiceLevel.Standard);

	private static ShippingRate CreateShippingRate() =>
		new(
			"ZONE-A",
			ShippingServiceLevel.Standard,
			new Money(5m, "INR"),
			new Money(2m, "INR"));

	private static MoneyOrder CreateMoneyOrder() =>
		new(
			new Money(100m, "INR"),
			"Sender",
			"Recipient",
			new Money(5m, "INR"));
}
