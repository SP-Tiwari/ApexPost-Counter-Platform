using System.Text.Json;
using System.Text.Json.Serialization;
using ApexPost.Application.Persistence;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.Notifications;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Infrastructure.Persistence;

public sealed class JsonApexPostRepository : IApexPostRepository, IDisposable
{
	private const int LegacySchemaVersion = 1;
	private const int CurrentSchemaVersion = 4;
	private readonly string _filePath;
	private readonly SemaphoreSlim _fileLock = new(1, 1);
	private readonly JsonSerializerOptions _serializerOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		RespectNullableAnnotations = true,
		RespectRequiredConstructorParameters = true,
		UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
		Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
	};

	public JsonApexPostRepository(string storageDirectory)
	{
		if (string.IsNullOrWhiteSpace(storageDirectory))
			throw new ArgumentException(
				"Storage directory is required.", nameof(storageDirectory));

		_filePath = Path.Combine(
			Path.GetFullPath(storageDirectory),
			"apexpost.json");
	}

	public void Dispose() => _fileLock.Dispose();

	public async Task<ApexPostSnapshot> LoadAsync(
		CancellationToken cancellationToken = default)
	{
		await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			return await LoadUnderLockAsync(cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			_fileLock.Release();
		}
	}

	public async Task SaveAsync(
		ApexPostSnapshot snapshot,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(snapshot);

		await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			await SaveUnderLockAsync(snapshot, cancellationToken)
				.ConfigureAwait(false);
		}
		finally
		{
			_fileLock.Release();
		}
	}

	public async Task UpdateAsync(
		Func<ApexPostSnapshot, ApexPostSnapshot> update,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(update);

		await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var current = await LoadUnderLockAsync(cancellationToken)
				.ConfigureAwait(false);
			var updated = update(current);
			ArgumentNullException.ThrowIfNull(updated);
			await SaveUnderLockAsync(updated, cancellationToken)
				.ConfigureAwait(false);
		}
		finally
		{
			_fileLock.Release();
		}
	}

	private async Task<ApexPostSnapshot> LoadUnderLockAsync(
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		FileStream stream;
		try
		{
			stream = new FileStream(
				_filePath,
				FileMode.Open,
				FileAccess.Read,
				FileShare.Read,
				4096,
				FileOptions.Asynchronous | FileOptions.SequentialScan);
		}
		catch (FileNotFoundException)
		{
			return ApexPostSnapshot.Empty;
		}
		catch (DirectoryNotFoundException)
		{
			return ApexPostSnapshot.Empty;
		}

		PersistedApexPost document;
		await using (stream)
		{
			var deserialized = await JsonSerializer.DeserializeAsync<PersistedApexPost>(
				stream,
				_serializerOptions,
				cancellationToken).ConfigureAwait(false);

			if (deserialized is null)
				throw new InvalidDataException(
					"Persisted ApexPost data cannot be null.");

			document = deserialized;
		}

		if (document.SchemaVersion < LegacySchemaVersion ||
			document.SchemaVersion > CurrentSchemaVersion)
			throw new InvalidDataException(
				$"Unsupported ApexPost data schema version '{document.SchemaVersion}'.");

		var isLegacySchema = document.SchemaVersion == LegacySchemaVersion;
		ValidateDocument(document, isLegacySchema);
		var snapshot = ToSnapshot(document);
		if (document.SchemaVersion != CurrentSchemaVersion)
			await SaveUnderLockAsync(snapshot, cancellationToken)
				.ConfigureAwait(false);

		return snapshot;
	}

	private async Task SaveUnderLockAsync(
		ApexPostSnapshot snapshot,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var document = ToDocument(snapshot);
		Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
		var temporaryPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";
		try
		{
			await using (var stream = new FileStream(
				temporaryPath,
				FileMode.CreateNew,
				FileAccess.Write,
				FileShare.None,
				4096,
				FileOptions.Asynchronous | FileOptions.SequentialScan))
			{
				await JsonSerializer.SerializeAsync(
					stream,
					document,
					_serializerOptions,
					cancellationToken).ConfigureAwait(false);
				await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
			}

			cancellationToken.ThrowIfCancellationRequested();
			File.Move(temporaryPath, _filePath, overwrite: true);
		}
		catch (Exception writeException)
		{
			try
			{
				File.Delete(temporaryPath);
			}
			catch (Exception cleanupException)
			{
				writeException.Data["TemporaryFileCleanupException"] =
					cleanupException;
			}

			throw;
		}
	}

	private static PersistedApexPost ToDocument(ApexPostSnapshot snapshot) =>
		new()
		{
			SchemaVersion = CurrentSchemaVersion,
			Branches = snapshot.Branches.Select(branch => new PersistedBranch
			{
				Code = branch.Code.Value,
				Name = branch.Name,
				Region = branch.Region,
				OpeningHours = branch.OpeningHours,
				IsActive = branch.IsActive,
				CounterTerminals = branch.CounterTerminals
					.Select(terminal => new PersistedCounterTerminal
					{
						Id = terminal.Id,
						Name = terminal.Name,
						IsActive = terminal.IsActive
					})
					.ToList()
			}).ToList(),
			Staff = snapshot.Staff.Select(staff => new PersistedStaff
			{
				Id = staff.Id,
				Name = staff.Name,
				Email = staff.Email,
				Role = staff.Role,
				HomeBranchCode = staff.HomeBranchCode.Value,
				IsActive = staff.IsActive
			}).ToList(),
			Products = snapshot.Products.Select(product => new PersistedProduct
			{
				Sku = product.Sku.Value,
				Name = product.Name,
				Category = product.Category,
				UnitPrice = ToMoney(product.UnitPrice),
				TaxCategory = product.TaxCategory
			}).ToList(),
			BranchStocks = snapshot.BranchStocks.Select(stock => new PersistedBranchStock
			{
				BranchCode = stock.BranchCode.Value,
				ProductSku = stock.ProductSku.Value,
				Quantity = stock.Quantity
			}).ToList(),
			ShippingItems = snapshot.ShippingItems.Select(item => new PersistedShippingItem
			{
				Id = item.Id,
				Type = item.Type,
				WeightKilograms = item.Weight.Kilograms,
				LengthCm = item.Dimensions.LengthCm,
				WidthCm = item.Dimensions.WidthCm,
				HeightCm = item.Dimensions.HeightCm,
				DestinationZone = item.DestinationZone,
				ServiceLevel = item.ServiceLevel
			}).ToList(),
			ShippingRates = snapshot.ShippingRates.Select(rate => new PersistedShippingRate
			{
				DestinationZone = rate.DestinationZone,
				ServiceLevel = rate.ServiceLevel,
				BasePrice = ToMoney(rate.BasePrice),
				PerKilogramCharge = ToMoney(rate.PerKilogramCharge)
			}).ToList(),
			MoneyOrders = snapshot.MoneyOrders.Select(order => new PersistedMoneyOrder
			{
				Amount = ToMoney(order.Amount),
				Sender = order.Sender,
				Recipient = order.Recipient,
				Fee = ToMoney(order.Fee),
				IdentityVerification = order.IdentityVerification is null
					? null
					: ToPersistedIdentityVerification(
						order.IdentityVerification)
			}).ToList(),
			IdentityVerifications = snapshot.IdentityVerifications
				.Select(ToPersistedIdentityVerification)
				.ToList(),
			TransactionBaskets = snapshot.TransactionBaskets
				.Select(ToPersistedBasket)
				.ToList()
		};

	private static PersistedTransactionBasket ToPersistedBasket(
		TransactionBasket basket) =>
		ToPersistedBasket(basket.GetSnapshot());

	private static PersistedTransactionBasket ToPersistedBasket(
		TransactionBasketSnapshot basket) =>
		new()
		{
			Id = basket.Id,
			Currency = basket.Currency,
			TaxRates = basket.TaxRates
				.Select(pair => new PersistedTaxRate
				{
					Category = pair.Key,
					Rate = pair.Value
				})
				.ToList(),
			LineItems = basket.LineItems.Select(lineItem => new PersistedLineItem
			{
				Description = lineItem.Description,
				Type = lineItem.Type,
				UnitPrice = ToMoney(lineItem.UnitPrice),
				Quantity = lineItem.Quantity,
				TaxCategory = lineItem.TaxCategory,
				ProductSku = lineItem.ProductSku?.Value,
				ShippingItem = lineItem.ShippingItem is null
					? null
					: ToPersistedShippingItem(lineItem.ShippingItem)
			}).ToList(),
			BranchCode = basket.BranchCode?.Value,
			PaymentStatus = basket.PaymentStatus,
			Notifications = basket.Notifications.Select(notification => notification switch
			{
				TransactionVoidedNotification voided => new PersistedNotification
				{
					Type = "voided",
					Reason = voided.Reason
				},
				TransactionRefundedNotification refunded => new PersistedNotification
				{
					Type = "refunded",
					Reason = refunded.Reason
				},
				_ => throw new InvalidDataException(
					$"Unsupported transaction notification type '{notification.GetType().Name}'.")
			}).ToList()
		};

	private static ApexPostSnapshot ToSnapshot(PersistedApexPost document)
	{
		try
		{
			return new ApexPostSnapshot(
				document.Branches.Select(ToBranch),
				document.Staff.Select(ToStaff),
				document.Products.Select(ToProduct),
				document.BranchStocks.Select(stock => new BranchStock(
					new BranchCode(stock.BranchCode),
					new Sku(stock.ProductSku),
					stock.Quantity)),
				document.ShippingItems.Select(ToShippingItem),
				document.ShippingRates.Select(rate => new ShippingRate(
					rate.DestinationZone,
					rate.ServiceLevel,
					ToMoney(rate.BasePrice),
					ToMoney(rate.PerKilogramCharge))),
				document.MoneyOrders.Select(order => new MoneyOrder(
					ToMoney(order.Amount),
					order.Sender,
					order.Recipient,
					ToMoney(order.Fee),
					order.IdentityVerification is null
						? null
						: ToIdentityVerification(order.IdentityVerification))),
				document.IdentityVerifications.Select(verification =>
					ToIdentityVerification(verification)),
				document.TransactionBaskets.Select(ToBasket));
		}

		catch (Exception exception) when (
			exception is ArgumentException or InvalidOperationException or
			OverflowException or KeyNotFoundException)
		{
			throw new InvalidDataException(
				"Persisted ApexPost data violates Domain invariants.", exception);
		}
	}

	private static PersistedIdentityVerification
		ToPersistedIdentityVerification(IdentityVerification verification) =>
		new()
		{
			DocumentType = verification.DocumentType,
			MaskedDocumentIdentifier = verification.MaskedDocumentIdentifier,
			IsSuccessful = verification.IsSuccessful
		};

	private static IdentityVerification ToIdentityVerification(
		PersistedIdentityVerification verification) =>
		IdentityVerification.FromMaskedIdentifier(
			verification.DocumentType,
			verification.MaskedDocumentIdentifier,
			verification.IsSuccessful);

	private static PersistedShippingItem ToPersistedShippingItem(
		ShippingItem item) =>
		new()
		{
			Id = item.Id,
			Type = item.Type,
			WeightKilograms = item.Weight.Kilograms,
			LengthCm = item.Dimensions.LengthCm,
			WidthCm = item.Dimensions.WidthCm,
			HeightCm = item.Dimensions.HeightCm,
			DestinationZone = item.DestinationZone,
			ServiceLevel = item.ServiceLevel
		};

	private static ShippingItem ToShippingItem(PersistedShippingItem item) =>
		new(
			item.Id,
			item.Type,
			new Weight(item.WeightKilograms),
			new Dimensions(item.LengthCm, item.WidthCm, item.HeightCm),
			item.DestinationZone,
			item.ServiceLevel);

	private static void ValidateDocument(
		PersistedApexPost document,
		bool allowMissingTransactionIds)
	{
		if (ContainsNull(document.Branches) ||
			ContainsNull(document.Staff) ||
			ContainsNull(document.Products) ||
			ContainsNull(document.BranchStocks) ||
			ContainsNull(document.ShippingItems) ||
			ContainsNull(document.ShippingRates) ||
			ContainsNull(document.MoneyOrders) ||
			ContainsNull(document.IdentityVerifications) ||
			ContainsNull(document.TransactionBaskets))
		{
			throw new InvalidDataException(
				"Persisted ApexPost data contains a null collection or record.");
		}

		var transactionIds = document.TransactionBaskets
			.Where(basket => basket.Id.HasValue)
			.Select(basket => basket.Id!.Value)
			.ToArray();
		if (transactionIds.Distinct().Count() != transactionIds.Length)
			throw new InvalidDataException(
				"Persisted transaction IDs must be unique.");

		if (document.Branches.Any(branch =>
				ContainsNull(branch.CounterTerminals)) ||
			document.Products.Any(product => product.UnitPrice is null) ||
			document.ShippingRates.Any(rate =>
				rate.BasePrice is null || rate.PerKilogramCharge is null) ||
			document.MoneyOrders.Any(order =>
				order.Amount is null || order.Fee is null) ||
			document.TransactionBaskets.Any(basket =>
				ContainsNull(basket.TaxRates) ||
				ContainsNull(basket.LineItems) ||
				ContainsNull(basket.Notifications) ||
				(!allowMissingTransactionIds &&
					(basket.Id is null || basket.Id == Guid.Empty)) ||
				basket.LineItems.Any(lineItem => lineItem.UnitPrice is null)))
		{
			throw new InvalidDataException(
				"Persisted ApexPost data is missing required nested records.");
		}
	}

	private static bool ContainsNull<T>(IEnumerable<T>? values)
		where T : class =>
		values is null || values.Any(value => value is null);

	private static Branch ToBranch(PersistedBranch branch)
	{
		var terminals = branch.CounterTerminals.Select(item =>
		{
			var terminal = new CounterTerminal(item.Id, item.Name);
			if (!item.IsActive)
				terminal.Deactivate();
			return terminal;
		});
		var restoredBranch = new Branch(
			new BranchCode(branch.Code),
			branch.Name,
			branch.Region,
			branch.OpeningHours,
			terminals);
		if (!branch.IsActive)
			restoredBranch.Close();
		return restoredBranch;
	}

	private static Staff ToStaff(PersistedStaff staff)
	{
		var restoredStaff = new Staff(
			staff.Id,
			staff.Name,
			staff.Email,
			staff.Role,
			new BranchCode(staff.HomeBranchCode));
		if (!staff.IsActive)
			restoredStaff.Deactivate();
		return restoredStaff;
	}

	private static Product ToProduct(PersistedProduct product) =>
		new(
			new Sku(product.Sku),
			product.Name,
			product.Category,
			ToMoney(product.UnitPrice),
			product.TaxCategory);

	private static TransactionBasket ToBasket(PersistedTransactionBasket basket)
	{
		var taxRates = basket.TaxRates.ToDictionary(
			rate => rate.Category,
			rate => rate.Rate);
		var lineItems = basket.LineItems.Select(item =>
			new TransactionLineItem(
				item.Description,
				item.Type,
				ToMoney(item.UnitPrice),
				item.Quantity,
				item.TaxCategory,
				item.ProductSku is null ? null : new Sku(item.ProductSku),
				item.ShippingItem is null
					? null
					: ToShippingItem(item.ShippingItem)));
		var notifications = basket.Notifications.Select(notification =>
			notification.Type switch
			{
				"voided" => (DomainNotification)new TransactionVoidedNotification(
					notification.Reason),
				"refunded" => new TransactionRefundedNotification(
					notification.Reason),
				_ => throw new InvalidDataException(
					$"Unsupported persisted transaction notification type '{notification.Type}'.")
			});

		return TransactionBasket.Reconstitute(
			basket.Id ?? Guid.NewGuid(),
			basket.Currency,
			taxRates,
			lineItems,
			basket.PaymentStatus,
			notifications,
			basket.BranchCode is null
				? null
				: new BranchCode(basket.BranchCode));
	}

	private static PersistedMoney ToMoney(Money money) =>
		new() { Amount = money.Amount, Currency = money.Currency };

	private static Money ToMoney(PersistedMoney money) =>
		new(money.Amount, money.Currency);
}

internal sealed class PersistedApexPost
{
	public required int SchemaVersion { get; set; }
	public required List<PersistedBranch> Branches { get; set; }
	public required List<PersistedStaff> Staff { get; set; }
	public required List<PersistedProduct> Products { get; set; }
	public required List<PersistedBranchStock> BranchStocks { get; set; }
	public required List<PersistedShippingItem> ShippingItems { get; set; }
	public required List<PersistedShippingRate> ShippingRates { get; set; }
	public required List<PersistedMoneyOrder> MoneyOrders { get; set; }
	public required List<PersistedIdentityVerification> IdentityVerifications { get; set; }
	public required List<PersistedTransactionBasket> TransactionBaskets { get; set; }
}

internal sealed class PersistedBranch
{
	public required string Code { get; set; }
	public required string Name { get; set; }
	public required string Region { get; set; }
	public required string OpeningHours { get; set; }
	public required bool IsActive { get; set; }
	public required List<PersistedCounterTerminal> CounterTerminals { get; set; }
}

internal sealed class PersistedCounterTerminal
{
	public required string Id { get; set; }
	public required string Name { get; set; }
	public required bool IsActive { get; set; }
}

internal sealed class PersistedStaff
{
	public required string Id { get; set; }
	public required string Name { get; set; }
	public required string Email { get; set; }
	public required StaffRole Role { get; set; }
	public required string HomeBranchCode { get; set; }
	public required bool IsActive { get; set; }
}

internal sealed class PersistedProduct
{
	public required string Sku { get; set; }
	public required string Name { get; set; }
	public required ProductCategory Category { get; set; }
	public required PersistedMoney UnitPrice { get; set; }
	public required TaxCategory TaxCategory { get; set; }
}

internal sealed class PersistedBranchStock
{
	public required string BranchCode { get; set; }
	public required string ProductSku { get; set; }
	public required int Quantity { get; set; }
}

internal sealed class PersistedShippingItem
{
	public required string Id { get; set; }
	public required ShippingItemType Type { get; set; }
	public required decimal WeightKilograms { get; set; }
	public required decimal LengthCm { get; set; }
	public required decimal WidthCm { get; set; }
	public required decimal HeightCm { get; set; }
	public required string DestinationZone { get; set; }
	public required ShippingServiceLevel ServiceLevel { get; set; }
}

internal sealed class PersistedShippingRate
{
	public required string DestinationZone { get; set; }
	public required ShippingServiceLevel ServiceLevel { get; set; }
	public required PersistedMoney BasePrice { get; set; }
	public required PersistedMoney PerKilogramCharge { get; set; }
}

internal sealed class PersistedMoneyOrder
{
	public required PersistedMoney Amount { get; set; }
	public required string Sender { get; set; }
	public required string Recipient { get; set; }
	public required PersistedMoney Fee { get; set; }
	public PersistedIdentityVerification? IdentityVerification { get; set; }
}

internal sealed class PersistedIdentityVerification
{
	public required string DocumentType { get; set; }
	public required string MaskedDocumentIdentifier { get; set; }
	public required bool IsSuccessful { get; set; }
}

internal sealed class PersistedMoney
{
	public required decimal Amount { get; set; }
	public required string Currency { get; set; }
}

internal sealed class PersistedTransactionBasket
{
	public Guid? Id { get; set; }
	public string? BranchCode { get; set; }
	public required string Currency { get; set; }
	public required List<PersistedTaxRate> TaxRates { get; set; }
	public required List<PersistedLineItem> LineItems { get; set; }
	public required TransactionPaymentStatus PaymentStatus { get; set; }
	public required List<PersistedNotification> Notifications { get; set; }
}

internal sealed class PersistedTaxRate
{
	public required TaxCategory Category { get; set; }
	public required decimal Rate { get; set; }
}

internal sealed class PersistedLineItem
{
	public required string Description { get; set; }
	public required TransactionItemType Type { get; set; }
	public required PersistedMoney UnitPrice { get; set; }
	public required int Quantity { get; set; }
	public required TaxCategory TaxCategory { get; set; }
	public required string? ProductSku { get; set; }
	public PersistedShippingItem? ShippingItem { get; set; }
}

internal sealed class PersistedNotification
{
	public required string Type { get; set; }
	public required string Reason { get; set; }
}
