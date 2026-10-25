using ApexPost.Domain.Services;

namespace ApexPost.Application.Services;

public sealed record GoodsBasketRequest(string Sku, int Quantity);

public sealed record ShippingBasketRequest(
	ShippingItemType Type,
	decimal WeightKilograms,
	decimal LengthCm,
	decimal WidthCm,
	decimal HeightCm,
	string DestinationZone,
	ShippingServiceLevel ServiceLevel);

public sealed record MixedBasketCatalog(
	IReadOnlyList<Product> Products,
	Branch Branch,
	IReadOnlyList<ShippingRate> ShippingRates);

public sealed class MixedBasketApplicationService
{
	private readonly IApexPostRepository _repository;
	private readonly IReadOnlyDictionary<TaxCategory, decimal> _taxRates;
	private readonly IReadOnlyDictionary<ShippingServiceLevel, Weight>
		_maximumParcelWeights;
	private readonly TaxCategory? _shippingTaxCategory;
	private readonly TransactionProcessingRule _processingRule = new();

	public MixedBasketApplicationService(
		IApexPostRepository repository,
		IReadOnlyDictionary<TaxCategory, decimal> taxRates,
		IReadOnlyDictionary<ShippingServiceLevel, Weight> maximumParcelWeights,
		TaxCategory? shippingTaxCategory = null)
	{
		ArgumentNullException.ThrowIfNull(repository);
		ArgumentNullException.ThrowIfNull(taxRates);
		ArgumentNullException.ThrowIfNull(maximumParcelWeights);
		_repository = repository;
		var validatedTaxRates = new Dictionary<TaxCategory, decimal>();
		foreach (var (category, rate) in taxRates)
		{
			if (!Enum.IsDefined(category))
				throw new ArgumentOutOfRangeException(nameof(taxRates));
			if (rate < 0m)
				throw new ArgumentOutOfRangeException(
					nameof(taxRates), "Tax rates cannot be negative.");
			validatedTaxRates.Add(category, rate);
		}
		_taxRates = validatedTaxRates;

		var validatedMaximumWeights =
			new Dictionary<ShippingServiceLevel, Weight>();
		foreach (var (serviceLevel, maximumWeight) in maximumParcelWeights)
		{
			if (!Enum.IsDefined(serviceLevel))
				throw new ArgumentOutOfRangeException(
					nameof(maximumParcelWeights));
			ArgumentNullException.ThrowIfNull(maximumWeight);
			validatedMaximumWeights.Add(serviceLevel, maximumWeight);
		}
		_maximumParcelWeights = validatedMaximumWeights;
		if (shippingTaxCategory is not null &&
			!Enum.IsDefined(shippingTaxCategory.Value))
			throw new ArgumentOutOfRangeException(nameof(shippingTaxCategory));
		_shippingTaxCategory = shippingTaxCategory;
	}

	public bool HasTaxRates =>
		_taxRates.Count > 0;

	public async Task<MixedBasketCatalog> GetCatalogAsync(
		string staffId,
		CancellationToken cancellationToken = default)
	{
		var snapshot = await _repository.LoadAsync(cancellationToken)
			.ConfigureAwait(false);
		var staff = FindActiveSalesStaff(snapshot, staffId);
		var branch = FindBranch(snapshot, staff.HomeBranchCode);
		if (!branch.IsActive)
			throw new InvalidOperationException(
				"Transactions cannot be processed at an inactive branch.");

		return new MixedBasketCatalog(
			Array.AsReadOnly(snapshot.Products
				.Where(product => _taxRates.ContainsKey(product.TaxCategory))
				.ToArray()),
			branch,
			Array.AsReadOnly(snapshot.ShippingRates
				.Where(_ => _shippingTaxCategory is not null &&
					_taxRates.ContainsKey(_shippingTaxCategory.Value))
				.ToArray()));
	}

	public async Task<Guid> CreateAndProcessAsync(
		Guid transactionId,
		string staffId,
		IReadOnlyList<GoodsBasketRequest> goods,
		IReadOnlyList<ShippingBasketRequest> shipments,
		CancellationToken cancellationToken = default)
	{
		if (transactionId == Guid.Empty)
			throw new ArgumentException(
				"Transaction ID cannot be empty.", nameof(transactionId));
		if (string.IsNullOrWhiteSpace(staffId))
			throw new UnauthorizedAccessException(
				"Authenticated staff are required to process a basket.");
		ArgumentNullException.ThrowIfNull(goods);
		ArgumentNullException.ThrowIfNull(shipments);
		if (goods.Count == 0 && shipments.Count == 0)
			throw new ArgumentException(
				"A transaction must contain at least one goods or shipping item.");

		foreach (var goodsRequest in goods)
		{
			ArgumentNullException.ThrowIfNull(goodsRequest);
			if (goodsRequest.Quantity <= 0)
				throw new ArgumentOutOfRangeException(
					nameof(goods), "Goods quantities must be positive.");
			if (string.IsNullOrWhiteSpace(goodsRequest.Sku))
				throw new ArgumentException(
					"Product SKU is required.", nameof(goods));
		}
		foreach (var shipment in shipments)
		{
			ArgumentNullException.ThrowIfNull(shipment);
			if (!Enum.IsDefined(shipment.Type) ||
				!Enum.IsDefined(shipment.ServiceLevel))
			{
				throw new ArgumentOutOfRangeException(
					nameof(shipments), "Shipping type or service level is invalid.");
			}
		}

		await _repository.UpdateAsync(snapshot =>
		{
			if (snapshot.TransactionBaskets.Any(
					basket => basket.Id == transactionId))
			{
				throw new InvalidOperationException(
					"This transaction submission has already been processed.");
			}

			var staff = FindActiveSalesStaff(snapshot, staffId);
			var branch = FindBranch(snapshot, staff.HomeBranchCode);
			var basket = new TransactionBasket(
				transactionId,
				ResolveCurrency(snapshot, goods, shipments),
				_taxRates,
				staff.HomeBranchCode);

			foreach (var goodsRequest in goods)
			{
				var product = FindProduct(snapshot, goodsRequest.Sku);
				basket.AddLineItem(new TransactionLineItem(
					product, goodsRequest.Quantity));
			}

			var pricingService = new ShippingPricingService(
				snapshot.ShippingRates);
			var shippingItems = new List<ShippingItem>(shipments.Count);
			var weightRule = new ShippingWeightLimitRule(
				_maximumParcelWeights);

			foreach (var shipment in shipments)
			{
				var shippingTaxCategory = _shippingTaxCategory
					?? throw new InvalidOperationException(
						"No shipping tax category is configured.");
				var shippingItem = new ShippingItem(
					Guid.NewGuid().ToString("N"),
					shipment.Type,
					new Weight(shipment.WeightKilograms),
					new Dimensions(
						shipment.LengthCm,
						shipment.WidthCm,
						shipment.HeightCm),
					shipment.DestinationZone,
					shipment.ServiceLevel);
				weightRule.EnsureWithinLimit(shippingItem);

				var price = pricingService.CalculatePrice(shippingItem);
				if (!string.Equals(
					price.Currency, basket.Currency, StringComparison.Ordinal))
				{
					throw new InvalidOperationException(
						"Shipping rate currency must match the transaction currency.");
				}

				basket.AddLineItem(new TransactionLineItem(
					$"Shipping {shippingItem.Type} to {shippingItem.DestinationZone} ({shippingItem.ServiceLevel})",
					TransactionItemType.Service,
					price,
					1,
					shippingTaxCategory,
					productSku: null,
					shippingItem: shippingItem));
				shippingItems.Add(shippingItem);
			}

			_processingRule.Process(
				staff, branch, basket, snapshot.BranchStocks);
			var basketTotal = basket.Total;
			if (basketTotal.Amount <= 0m)
				throw new InvalidOperationException(
					"Transaction total must be greater than zero.");

			return new ApexPostSnapshot(
				snapshot.Branches,
				snapshot.Staff,
				snapshot.Products,
				snapshot.BranchStocks,
				snapshot.ShippingItems.Concat(shippingItems),
				snapshot.ShippingRates,
				snapshot.MoneyOrders,
				snapshot.IdentityVerifications,
				snapshot.TransactionBaskets.Append(basket));
		}, cancellationToken).ConfigureAwait(false);
		return transactionId;
	}

	private string ResolveCurrency(
		ApexPostSnapshot snapshot,
		IReadOnlyList<GoodsBasketRequest> goods,
		IReadOnlyList<ShippingBasketRequest> shipments)
	{
		var currency = goods.Count > 0
			? FindProduct(snapshot, goods[0].Sku).UnitPrice.Currency
			: FindRate(snapshot, shipments[0]).BasePrice.Currency;

		foreach (var goodsRequest in goods)
		{
			if (!string.Equals(
				FindProduct(snapshot, goodsRequest.Sku).UnitPrice.Currency,
				currency,
				StringComparison.Ordinal))
			{
				throw new InvalidOperationException(
					"All transaction items must use the same currency.");
			}
		}

		foreach (var shippingRequest in shipments)
		{
			if (!string.Equals(
				FindRate(snapshot, shippingRequest).BasePrice.Currency,
				currency,
				StringComparison.Ordinal))
			{
				throw new InvalidOperationException(
					"All transaction items must use the same currency.");
			}
		}

		return currency;
	}

	private static Product FindProduct(
		ApexPostSnapshot snapshot,
		string sku)
	{
		if (string.IsNullOrWhiteSpace(sku))
			throw new ArgumentException("Product SKU is required.", nameof(sku));
		var matches = snapshot.Products.Where(product =>
			string.Equals(product.Sku.Value, sku, StringComparison.Ordinal))
			.Take(2).ToArray();
		return matches.Length switch
		{
			1 => matches[0],
			0 => throw new KeyNotFoundException(
				$"Product '{sku}' was not found."),
			_ => throw new InvalidOperationException(
				$"Multiple products match SKU '{sku}'.")
		};
	}

	private static Staff FindActiveSalesStaff(
		ApexPostSnapshot snapshot,
		string staffId)
	{
		if (string.IsNullOrWhiteSpace(staffId))
			throw new UnauthorizedAccessException(
				"Authenticated staff are required to process transactions.");
		var matches = snapshot.Staff.Where(person =>
			string.Equals(person.Id, staffId, StringComparison.Ordinal))
			.Take(2).ToArray();
		if (matches.Length != 1 ||
			!matches[0].IsActive ||
			matches[0].Role is not StaffRole.CounterClerk and
				not StaffRole.BranchManager)
		{
			throw new UnauthorizedAccessException(
				"An active Counter Clerk or Branch Manager is required to process transactions.");
		}

		return matches[0];
	}

	private static Branch FindBranch(
		ApexPostSnapshot snapshot,
		BranchCode branchCode)
	{
		var matches = snapshot.Branches.Where(branch =>
			branch.Code == branchCode).Take(2).ToArray();
		return matches.Length switch
		{
			1 => matches[0],
			0 => throw new KeyNotFoundException(
				$"Branch '{branchCode}' was not found."),
			_ => throw new InvalidOperationException(
				$"Multiple branches match '{branchCode}'.")
		};
	}

	private static ShippingRate FindRate(
		ApexPostSnapshot snapshot,
		ShippingBasketRequest request)
	{
		if (string.IsNullOrWhiteSpace(request.DestinationZone))
			throw new ArgumentException(
				"Destination zone is required.", nameof(request));

		return snapshot.ShippingRates.SingleOrDefault(rate =>
			string.Equals(
				rate.DestinationZone,
				request.DestinationZone.Trim(),
				StringComparison.Ordinal) &&
			rate.ServiceLevel == request.ServiceLevel)
			?? throw new InvalidOperationException(
				"No configured shipping rate matches the destination and service level.");
	}
}
