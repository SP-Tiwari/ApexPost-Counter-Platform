using ApexPost.Application.Events;
using ApexPost.Application.Validation;
using ApexPost.Application.Verification;
using ApexPost.Domain.Events;
using ApexPost.Domain.Services;

namespace ApexPost.Application.Services;

public sealed record GoodsBasketRequest(
	[property: NotBlank] string Sku,
	[property: GreaterThanZero] int Quantity);

// Weight and dimensions are validated by the Weight and Dimensions value
// objects; the request only checks what the domain types cannot.
public sealed record ShippingBasketRequest(
	[property: DefinedEnum] ShippingItemType Type,
	decimal WeightKilograms,
	decimal LengthCm,
	decimal WidthCm,
	decimal HeightCm,
	[property: NotBlank] string DestinationZone,
	[property: DefinedEnum] ShippingServiceLevel ServiceLevel);

// A complete counter sale from the transaction screen: any number of goods,
// shipping and money-order lines, taken at one counter with one payment method.
public sealed record CounterBasketRequest(
	Guid TransactionId,
	[property: NotBlank] string CounterTerminalId,
	[property: NotBlank] string PaymentMethod,
	IReadOnlyList<GoodsBasketRequest> Goods,
	IReadOnlyList<ShippingBasketRequest> Shipments,
	IReadOnlyList<MoneyOrderBasketRequest> MoneyOrders);

public sealed record MixedBasketCatalog(
	IReadOnlyList<Product> Products,
	Branch Branch,
	IReadOnlyList<ShippingRate> ShippingRates,
	MoneyOrderBasketSettings? MoneyOrders = null);

public sealed class MixedBasketApplicationService
{
	private readonly IApexPostRepository _repository;
	private readonly IReadOnlyDictionary<TaxCategory, decimal> _taxRates;
	private readonly IReadOnlyDictionary<ShippingServiceLevel, Weight>
		_maximumParcelWeights;
	private readonly TaxCategory? _shippingTaxCategory;
	private readonly TransactionProcessingRule _processingRule = new();
	private readonly TimeProvider _timeProvider;
	private readonly MoneyOrderBasketSettings? _moneyOrderSettings;
	private readonly MoneyOrderIdentityVerificationRule? _moneyOrderRule;
	private readonly IIdentityVerificationService? _identityVerification;
	private readonly HighValueMoneyOrderNotificationRule? _highValueRule;
	private readonly DomainEventPublisher? _events;
	private readonly int? _lowStockThreshold;
	private readonly IReadOnlyList<string> _paymentMethods;

	public MixedBasketApplicationService(
		IApexPostRepository repository,
		IReadOnlyDictionary<TaxCategory, decimal> taxRates,
		IReadOnlyDictionary<ShippingServiceLevel, Weight> maximumParcelWeights,
		TaxCategory? shippingTaxCategory = null,
		TimeProvider? timeProvider = null,
		MoneyOrderBasketSettings? moneyOrderSettings = null,
		IIdentityVerificationService? identityVerification = null,
		DomainEventPublisher? events = null,
		int? lowStockThreshold = null,
		IReadOnlyCollection<string>? paymentMethods = null)
	{
		ArgumentNullException.ThrowIfNull(repository);
		ArgumentNullException.ThrowIfNull(taxRates);
		ArgumentNullException.ThrowIfNull(maximumParcelWeights);
		_repository = repository;
		_timeProvider = timeProvider ?? TimeProvider.System;
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
		_moneyOrderSettings = moneyOrderSettings;
		_moneyOrderRule = moneyOrderSettings is null
			? null
			: new MoneyOrderIdentityVerificationRule(
				moneyOrderSettings.HighValueThreshold);
		_identityVerification = identityVerification;
		_highValueRule = moneyOrderSettings is null
			? null
			: new HighValueMoneyOrderNotificationRule(
				moneyOrderSettings.HighValueThreshold);
		_events = events;
		if (lowStockThreshold < 0)
			throw new ArgumentOutOfRangeException(
				nameof(lowStockThreshold), "Low-stock threshold cannot be negative.");
		_lowStockThreshold = lowStockThreshold;

		var validatedPaymentMethods = new List<string>();
		foreach (var method in paymentMethods ?? [])
		{
			if (string.IsNullOrWhiteSpace(method))
				throw new ArgumentException(
					"Payment method names cannot be blank.", nameof(paymentMethods));
			var name = method.Trim();
			if (validatedPaymentMethods.Contains(
				name, StringComparer.OrdinalIgnoreCase))
			{
				throw new ArgumentException(
					$"Payment method '{name}' is configured more than once.",
					nameof(paymentMethods));
			}
			validatedPaymentMethods.Add(name);
		}
		_paymentMethods = validatedPaymentMethods.AsReadOnly();
	}

	public bool HasTaxRates =>
		_taxRates.Count > 0;

	// Configured payment method names; counter sales cannot be taken until
	// at least one is configured.
	public IReadOnlyList<string> PaymentMethods => _paymentMethods;

	private string ResolvePaymentMethod(string? paymentMethod)
	{
		if (_paymentMethods.Count == 0)
			throw new InvalidOperationException(
				"No payment methods are configured.");
		if (string.IsNullOrWhiteSpace(paymentMethod))
			throw new ArgumentException(
				"Select a payment method.", nameof(paymentMethod));

		return _paymentMethods.FirstOrDefault(configured => string.Equals(
				configured, paymentMethod.Trim(), StringComparison.OrdinalIgnoreCase))
			?? throw new ArgumentException(
				"Select a configured payment method.", nameof(paymentMethod));
	}

	// Money orders need configured settings and a tax rate for the fee.
	public bool MoneyOrdersAvailable =>
		_moneyOrderSettings is not null &&
		_taxRates.ContainsKey(_moneyOrderSettings.FeeTaxCategory);

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
				.ToArray()),
			MoneyOrdersAvailable ? _moneyOrderSettings : null);
	}

	// Records no counter, clerk, or payment method; counter sales use the
	// CounterBasketRequest overload.
	public Task<Guid> CreateAndProcessAsync(
		Guid transactionId,
		string staffId,
		IReadOnlyList<GoodsBasketRequest> goods,
		IReadOnlyList<ShippingBasketRequest> shipments,
		CancellationToken cancellationToken = default) =>
		ProcessAsync(
			transactionId,
			staffId,
			goods,
			shipments,
			[],
			counterTerminalId: null,
			paymentMethod: null,
			cancellationToken);

	public Task<Guid> CreateAndProcessAsync(
		string staffId,
		CounterBasketRequest request,
		CancellationToken cancellationToken = default)
	{
		RequestValidator.Validate(request);

		return ProcessAsync(
			request.TransactionId,
			staffId,
			request.Goods,
			request.Shipments,
			request.MoneyOrders,
			request.CounterTerminalId,
			ResolvePaymentMethod(request.PaymentMethod),
			cancellationToken);
	}

	private async Task<Guid> ProcessAsync(
		Guid transactionId,
		string staffId,
		IReadOnlyList<GoodsBasketRequest> goods,
		IReadOnlyList<ShippingBasketRequest> shipments,
		IReadOnlyList<MoneyOrderBasketRequest> moneyOrders,
		string? counterTerminalId,
		string? paymentMethod,
		CancellationToken cancellationToken)
	{
		if (transactionId == Guid.Empty)
			throw new ArgumentException(
				"Transaction ID cannot be empty.", nameof(transactionId));
		if (string.IsNullOrWhiteSpace(staffId))
			throw new UnauthorizedAccessException(
				"Authenticated staff are required to process a basket.");
		ArgumentNullException.ThrowIfNull(goods);
		ArgumentNullException.ThrowIfNull(shipments);
		ArgumentNullException.ThrowIfNull(moneyOrders);
		if (goods.Count == 0 && shipments.Count == 0 && moneyOrders.Count == 0)
			throw new ArgumentException(
				"A transaction must contain at least one goods, shipping, or money-order item.");
		RequestValidator.ValidateAll(goods);
		RequestValidator.ValidateAll(shipments);
		RequestValidator.ValidateAll(moneyOrders);
		foreach (var moneyOrder in moneyOrders)
		{
			// A cross-field rule, so it stays explicit rather than an attribute.
			if (string.IsNullOrWhiteSpace(moneyOrder.DocumentType) !=
				string.IsNullOrWhiteSpace(moneyOrder.DocumentIdentifier))
			{
				throw new ArgumentException(
					"Enter both the identity document type and number, or neither.",
					nameof(moneyOrders));
			}
		}
		if (moneyOrders.Count > 0 && !MoneyOrdersAvailable)
			throw new InvalidOperationException(
				"Money orders are not configured.");

		// The verification provider is asynchronous and possibly slow, so it
		// runs before the repository lock is taken. The decision is applied
		// inside the atomic update below.
		var verificationResults = await VerifyIdentitiesAsync(
			staffId, moneyOrders, cancellationToken).ConfigureAwait(false);

		// Collected inside the update; published only once it has been saved.
		IReadOnlyList<DomainEvent> committedEvents = [];
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
			var occurredAtUtc = _timeProvider.GetUtcNow();
			// The clerk is the authenticated staff member; the domain rule
			// checks the counter belongs to their branch and is active.
			var basket = new TransactionBasket(
				transactionId,
				ResolveCurrency(snapshot, goods, shipments, moneyOrders),
				_taxRates,
				staff.HomeBranchCode,
				occurredAtUtc,
				counterTerminalId is null
					? null
					: new CounterSaleDetails(
						counterTerminalId, staff.Id, paymentMethod!));

			var issuedMoneyOrders = new List<MoneyOrder>(moneyOrders.Count);
			for (var index = 0; index < moneyOrders.Count; index++)
			{
				var moneyOrder = CreateMoneyOrder(
					moneyOrders[index],
					verificationResults[index],
					staff.HomeBranchCode);
				basket.AddLineItem(new TransactionLineItem(moneyOrder));
				basket.AddLineItem(new TransactionLineItem(
					"Money order fee",
					TransactionItemType.Service,
					moneyOrder.Fee,
					1,
					_moneyOrderSettings!.FeeTaxCategory));
				issuedMoneyOrders.Add(moneyOrder);
			}

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

			var quantitiesBefore = LowStockEvents.CaptureQuantities(
				snapshot.BranchStocks,
				branch.Code,
				basket.LineItems
					.Select(lineItem => lineItem.ProductSku)
					.OfType<Sku>());
			_processingRule.Process(
				staff, branch, basket, snapshot.BranchStocks);
			var basketTotal = basket.Total;
			if (basketTotal.Amount <= 0m)
				throw new InvalidOperationException(
					"Transaction total must be greater than zero.");

			committedEvents = CreateSaleEvents(
				basket,
				staffId,
				issuedMoneyOrders,
				snapshot.BranchStocks,
				quantitiesBefore,
				occurredAtUtc);
			return new ApexPostSnapshot(
				snapshot.Branches,
				snapshot.Staff,
				snapshot.Products,
				snapshot.BranchStocks,
				snapshot.ShippingItems.Concat(shippingItems),
				snapshot.ShippingRates,
				snapshot.MoneyOrders.Concat(issuedMoneyOrders),
				snapshot.IdentityVerifications.Concat(issuedMoneyOrders
					.Select(order => order.IdentityVerification)
					.OfType<IdentityVerification>()),
				snapshot.TransactionBaskets.Append(basket));
		}, cancellationToken).ConfigureAwait(false);

		_events?.Publish(committedEvents);
		return transactionId;
	}

	private IReadOnlyList<DomainEvent> CreateSaleEvents(
		TransactionBasket basket,
		string staffId,
		IReadOnlyList<MoneyOrder> issuedMoneyOrders,
		IEnumerable<BranchStock> branchStocks,
		IReadOnlyDictionary<Sku, int> quantitiesBefore,
		DateTimeOffset occurredAtUtc)
	{
		var branchCode = basket.BranchCode!;
		var events = new List<DomainEvent>
		{
			new TransactionCompletedEvent(
				basket.Id, branchCode, staffId, basket.Total, occurredAtUtc)
		};

		events.AddRange(LowStockEvents.Evaluate(
			branchStocks,
			branchCode,
			quantitiesBefore,
			_lowStockThreshold,
			occurredAtUtc));

		foreach (var moneyOrder in issuedMoneyOrders)
		{
			events.Add(new MoneyOrderIssuedEvent(
				moneyOrder.Id,
				basket.Id,
				branchCode,
				moneyOrder.Amount,
				occurredAtUtc));
			if (_highValueRule?.Evaluate(moneyOrder) is { } highValue)
			{
				events.Add(new HighValueMoneyOrderIssuedEvent(
					moneyOrder.Id, basket.Id, highValue, occurredAtUtc));
			}
		}

		return events;
	}

	private async Task<bool?[]> VerifyIdentitiesAsync(
		string staffId,
		IReadOnlyList<MoneyOrderBasketRequest> moneyOrders,
		CancellationToken cancellationToken)
	{
		var results = new bool?[moneyOrders.Count];
		if (moneyOrders.All(order => string.IsNullOrWhiteSpace(order.DocumentType)))
			return results;

		if (_identityVerification is null)
			throw new IdentityVerificationUnavailableException(
				"Identity verification is not configured.");

		// Only active sales staff may use the verification provider. The
		// update re-checks staff authoritatively under the repository lock.
		var snapshot = await _repository.LoadAsync(cancellationToken)
			.ConfigureAwait(false);
		FindActiveSalesStaff(snapshot, staffId);

		for (var index = 0; index < moneyOrders.Count; index++)
		{
			var request = moneyOrders[index];
			if (string.IsNullOrWhiteSpace(request.DocumentType))
				continue;

			results[index] = await _identityVerification.VerifyAsync(
				request.DocumentType,
				request.DocumentIdentifier!,
				cancellationToken).ConfigureAwait(false);
		}

		return results;
	}

	private MoneyOrder CreateMoneyOrder(
		MoneyOrderBasketRequest request,
		bool? verificationResult,
		BranchCode branchCode)
	{
		var settings = _moneyOrderSettings!;
		// The full document identifier is masked by IdentityVerification and
		// is not kept anywhere else.
		var verification = verificationResult is bool isSuccessful
			? new IdentityVerification(
				request.DocumentType!,
				request.DocumentIdentifier!,
				isSuccessful)
			: null;
		var moneyOrder = new MoneyOrder(
			new Money(request.Amount, settings.HighValueThreshold.Currency),
			request.Sender,
			request.Recipient,
			settings.Fee,
			verification,
			branchCode);
		_moneyOrderRule!.EnsureVerification(moneyOrder, verification);
		return moneyOrder;
	}

	private string ResolveCurrency(
		ApexPostSnapshot snapshot,
		IReadOnlyList<GoodsBasketRequest> goods,
		IReadOnlyList<ShippingBasketRequest> shipments,
		IReadOnlyList<MoneyOrderBasketRequest> moneyOrders)
	{
		var currency = goods.Count > 0
			? FindProduct(snapshot, goods[0].Sku).UnitPrice.Currency
			: shipments.Count > 0
				? FindRate(snapshot, shipments[0]).BasePrice.Currency
				: _moneyOrderSettings!.HighValueThreshold.Currency;

		if (moneyOrders.Count > 0 &&
			!string.Equals(
				_moneyOrderSettings!.HighValueThreshold.Currency,
				currency,
				StringComparison.Ordinal))
		{
			throw new InvalidOperationException(
				"All transaction items must use the same currency.");
		}

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
