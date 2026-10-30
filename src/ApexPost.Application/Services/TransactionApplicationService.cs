using ApexPost.Application.Events;
using ApexPost.Domain.Events;

namespace ApexPost.Application.Services;

public sealed class TransactionApplicationService
{
	private readonly IApexPostRepository _repository;
	private readonly TransactionProcessingRule _processingRule = new();
	private readonly TransactionAccessRule _accessRule = new();
	private readonly TransactionVoidRefundRule _voidRefundRule = new();
	private readonly TimeProvider _timeProvider;
	private readonly DomainEventPublisher? _events;
	private readonly int? _lowStockThreshold;

	public TransactionApplicationService(
		IApexPostRepository repository,
		TimeProvider? timeProvider = null,
		DomainEventPublisher? events = null,
		int? lowStockThreshold = null)
	{
		ArgumentNullException.ThrowIfNull(repository);
		if (lowStockThreshold < 0)
			throw new ArgumentOutOfRangeException(
				nameof(lowStockThreshold), "Low-stock threshold cannot be negative.");
		_repository = repository;
		_timeProvider = timeProvider ?? TimeProvider.System;
		_events = events;
		_lowStockThreshold = lowStockThreshold;
	}

	public async Task<IReadOnlyList<TransactionBasketSnapshot>>
		GetTransactionBasketsAsync(
			string staffId,
			CancellationToken cancellationToken = default)
	{
		var snapshot = await _repository.LoadAsync(cancellationToken)
			.ConfigureAwait(false);
		var staff = FindActiveBranchManager(snapshot, staffId);
		return Array.AsReadOnly(snapshot.TransactionBaskets
			.Where(basket => _accessRule.CanAccess(staff, basket))
			.Select(basket => basket.GetSnapshot())
			.ToArray());
	}

	public async Task<TransactionBasketSnapshot> GetTransactionBasketAsync(
		Guid transactionId,
		string staffId,
		CancellationToken cancellationToken = default)
	{
		if (transactionId == Guid.Empty)
			throw new ArgumentException(
				"Transaction ID cannot be empty.", nameof(transactionId));

		var snapshot = await _repository.LoadAsync(cancellationToken)
			.ConfigureAwait(false);
		var staff = FindActiveTransactionViewer(snapshot, staffId);
		var basket = FindBasket(snapshot, transactionId);
		if (!_accessRule.CanView(staff, basket))
			throw new KeyNotFoundException(
				$"Transaction '{transactionId}' was not found.");
		return basket.GetSnapshot();
	}

	public async Task<Guid> CreateBasketAsync(
		string currency,
		IReadOnlyDictionary<TaxCategory, decimal> taxRates,
		CancellationToken cancellationToken = default)
	{
		var basket = new TransactionBasket(
			currency,
			taxRates,
			occurredAtUtc: _timeProvider.GetUtcNow());
		await _repository.UpdateAsync(snapshot =>
		{
			if (snapshot.TransactionBaskets.Any(
					existing => existing.Id == basket.Id))
			{
				throw new InvalidOperationException(
					$"Transaction '{basket.Id}' already exists.");
			}

			return new ApexPostSnapshot(
				snapshot.Branches,
				snapshot.Staff,
				snapshot.Products,
				snapshot.BranchStocks,
				snapshot.ShippingItems,
				snapshot.ShippingRates,
				snapshot.MoneyOrders,
				snapshot.IdentityVerifications,
				snapshot.TransactionBaskets.Append(basket));
		}, cancellationToken).ConfigureAwait(false);

		return basket.Id;
	}

	public Task AddLineItemAsync(
		Guid transactionId,
		TransactionLineItem lineItem,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(lineItem);
		return _repository.UpdateAsync(snapshot =>
		{
			FindBasket(snapshot, transactionId).AddLineItem(lineItem);
			return snapshot;
		}, cancellationToken);
	}

	public async Task ProcessSaleAsync(
		Guid transactionId,
		string staffId,
		BranchCode branchCode,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(branchCode);
		// Collected inside the update; published only once it has been saved.
		IReadOnlyList<DomainEvent> committedEvents = [];
		await _repository.UpdateAsync(snapshot =>
		{
			var basket = FindBasket(snapshot, transactionId);
			var staff = FindStaff(snapshot, staffId);
			var branch = FindExactly(
				snapshot.Branches,
				candidate => candidate.Code == branchCode,
				$"Branch '{branchCode}'");
			var quantitiesBefore = LowStockEvents.CaptureQuantities(
				snapshot.BranchStocks,
				branch.Code,
				basket.LineItems
					.Select(lineItem => lineItem.ProductSku)
					.OfType<Sku>());
			_processingRule.Process(
				staff, branch, basket, snapshot.BranchStocks);

			var occurredAtUtc = _timeProvider.GetUtcNow();
			committedEvents =
			[
				new TransactionCompletedEvent(
					basket.Id, branch.Code, staffId, basket.Total, occurredAtUtc),
				.. LowStockEvents.Evaluate(
					snapshot.BranchStocks,
					branch.Code,
					quantitiesBefore,
					_lowStockThreshold,
					occurredAtUtc)
			];
			return snapshot;
		}, cancellationToken).ConfigureAwait(false);

		_events?.Publish(committedEvents);
	}

	public async Task VoidAsync(
		Guid transactionId,
		string staffId,
		string reason,
		CancellationToken cancellationToken = default)
	{
		TransactionVoidedEvent? committedEvent = null;
		await _repository.UpdateAsync(snapshot =>
		{
			var basket = FindBasket(snapshot, transactionId);
			var staff = FindActiveBranchManager(snapshot, staffId);
			if (!_accessRule.CanAccess(staff, basket))
				throw new KeyNotFoundException(
					$"Transaction '{transactionId}' was not found.");
			_voidRefundRule.Void(
				staff, basket, reason, snapshot.BranchStocks);
			committedEvent = new TransactionVoidedEvent(
				basket.Id,
				basket.BranchCode,
				staffId,
				reason.Trim(),
				_timeProvider.GetUtcNow());
			return snapshot;
		}, cancellationToken).ConfigureAwait(false);

		_events?.Publish([committedEvent!]);
	}

	public async Task RefundAsync(
		Guid transactionId,
		string staffId,
		string reason,
		CancellationToken cancellationToken = default)
	{
		TransactionRefundedEvent? committedEvent = null;
		await _repository.UpdateAsync(snapshot =>
		{
			var basket = FindBasket(snapshot, transactionId);
			var staff = FindActiveBranchManager(snapshot, staffId);
			if (!_accessRule.CanAccess(staff, basket))
				throw new KeyNotFoundException(
					$"Transaction '{transactionId}' was not found.");
			_voidRefundRule.Refund(
				staff, basket, reason, snapshot.BranchStocks);
			committedEvent = new TransactionRefundedEvent(
				basket.Id,
				basket.BranchCode,
				staffId,
				reason.Trim(),
				_timeProvider.GetUtcNow());
			return snapshot;
		}, cancellationToken).ConfigureAwait(false);

		_events?.Publish([committedEvent!]);
	}

	private static TransactionBasket FindBasket(
		ApexPostSnapshot snapshot,
		Guid transactionId)
	{
		if (transactionId == Guid.Empty)
			throw new ArgumentException(
				"Transaction ID cannot be empty.", nameof(transactionId));

		return FindExactly(
			snapshot.TransactionBaskets,
			basket => basket.Id == transactionId,
			$"Transaction '{transactionId}'");
	}

	private static Staff FindStaff(ApexPostSnapshot snapshot, string staffId)
	{
		if (string.IsNullOrWhiteSpace(staffId))
			throw new ArgumentException("Staff ID is required.", nameof(staffId));

		return FindExactly(
			snapshot.Staff,
			person => string.Equals(
				person.Id, staffId, StringComparison.Ordinal),
			$"Staff '{staffId}'");
	}

	private static Staff FindActiveBranchManager(
		ApexPostSnapshot snapshot,
		string staffId)
	{
		if (string.IsNullOrWhiteSpace(staffId))
			throw new UnauthorizedAccessException(
				"An active Branch Manager is required.");
		var matches = snapshot.Staff.Where(person =>
			string.Equals(
				person.Id, staffId, StringComparison.Ordinal))
			.Take(2).ToArray();
		if (matches.Length != 1 ||
			!matches[0].IsActive ||
			matches[0].Role != StaffRole.BranchManager)
		{
			throw new UnauthorizedAccessException(
				"An active Branch Manager is required.");
		}

		return matches[0];
	}

	// Branch Managers and Counter Clerks may read a transaction; which ones
	// each can see is decided by TransactionAccessRule.CanView.
	private static Staff FindActiveTransactionViewer(
		ApexPostSnapshot snapshot,
		string staffId)
	{
		if (string.IsNullOrWhiteSpace(staffId))
			throw new UnauthorizedAccessException(
				"An active Branch Manager or Counter Clerk is required.");
		var matches = snapshot.Staff.Where(person =>
			string.Equals(
				person.Id, staffId, StringComparison.Ordinal))
			.Take(2).ToArray();
		if (matches.Length != 1 ||
			!matches[0].IsActive ||
			matches[0].Role is not (StaffRole.BranchManager or StaffRole.CounterClerk))
		{
			throw new UnauthorizedAccessException(
				"An active Branch Manager or Counter Clerk is required.");
		}

		return matches[0];
	}

	private static T FindExactly<T>(
		IEnumerable<T> entries,
		Func<T, bool> predicate,
		string description)
	{
		var matches = entries.Where(predicate).Take(2).ToArray();
		return matches.Length switch
		{
			0 => throw new KeyNotFoundException($"{description} was not found."),
			1 => matches[0],
			_ => throw new InvalidOperationException(
				$"Multiple records match {description}.")
		};
	}
}
