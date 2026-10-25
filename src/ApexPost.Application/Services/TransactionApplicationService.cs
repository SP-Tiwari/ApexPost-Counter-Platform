using ApexPost.Application.Persistence;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.Rules;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Application.Services;

public sealed class TransactionApplicationService
{
	private readonly IApexPostRepository _repository;
	private readonly TransactionProcessingRule _processingRule = new();
	private readonly TransactionAccessRule _accessRule = new();
	private readonly TransactionVoidRefundRule _voidRefundRule = new();

	public TransactionApplicationService(IApexPostRepository repository)
	{
		ArgumentNullException.ThrowIfNull(repository);
		_repository = repository;
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
		var staff = FindActiveBranchManager(snapshot, staffId);
		var basket = FindBasket(snapshot, transactionId);
		if (!_accessRule.CanAccess(staff, basket))
			throw new KeyNotFoundException(
				$"Transaction '{transactionId}' was not found.");
		return basket.GetSnapshot();
	}

	public async Task<Guid> CreateBasketAsync(
		string currency,
		IReadOnlyDictionary<TaxCategory, decimal> taxRates,
		CancellationToken cancellationToken = default)
	{
		var basket = new TransactionBasket(currency, taxRates);
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

	public Task ProcessSaleAsync(
		Guid transactionId,
		string staffId,
		BranchCode branchCode,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(branchCode);
		return _repository.UpdateAsync(snapshot =>
		{
			var basket = FindBasket(snapshot, transactionId);
			var staff = FindStaff(snapshot, staffId);
			var branch = FindExactly(
				snapshot.Branches,
				candidate => candidate.Code == branchCode,
				$"Branch '{branchCode}'");
			_processingRule.Process(
				staff, branch, basket, snapshot.BranchStocks);
			return snapshot;
		}, cancellationToken);
	}

	public Task VoidAsync(
		Guid transactionId,
		string staffId,
		string reason,
		CancellationToken cancellationToken = default) =>
		_repository.UpdateAsync(snapshot =>
		{
			var basket = FindBasket(snapshot, transactionId);
			var staff = FindActiveBranchManager(snapshot, staffId);
			if (!_accessRule.CanAccess(staff, basket))
				throw new KeyNotFoundException(
					$"Transaction '{transactionId}' was not found.");
			_voidRefundRule.Void(staff, basket, reason);
			return snapshot;
		}, cancellationToken);

	public Task RefundAsync(
		Guid transactionId,
		string staffId,
		string reason,
		CancellationToken cancellationToken = default) =>
		_repository.UpdateAsync(snapshot =>
		{
			var basket = FindBasket(snapshot, transactionId);
			var staff = FindActiveBranchManager(snapshot, staffId);
			if (!_accessRule.CanAccess(staff, basket))
				throw new KeyNotFoundException(
					$"Transaction '{transactionId}' was not found.");
			_voidRefundRule.Refund(staff, basket, reason);
			return snapshot;
		}, cancellationToken);

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
