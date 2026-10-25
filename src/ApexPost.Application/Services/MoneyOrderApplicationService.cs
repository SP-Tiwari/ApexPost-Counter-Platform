using ApexPost.Application.Persistence;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.Rules;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Application.Services;

public sealed class MoneyOrderApplicationService
{
	private readonly IApexPostRepository _repository;
	private readonly Money? _highValueThreshold;
	private readonly MoneyOrderIdentityVerificationRule? _verificationRule;

	public MoneyOrderApplicationService(
		IApexPostRepository repository,
		Money? highValueThreshold)
	{
		ArgumentNullException.ThrowIfNull(repository);
		_repository = repository;
		_highValueThreshold = highValueThreshold;
		_verificationRule = highValueThreshold is null
			? null
			: new MoneyOrderIdentityVerificationRule(highValueThreshold);
	}

	public bool IsConfigured => _verificationRule is not null;

	public string? Currency => _verificationRule is null
		? null
		: _highValueThreshold!.Currency;

	public async Task<IReadOnlyList<MoneyOrder>> GetMoneyOrdersAsync(
		CancellationToken cancellationToken = default)
	{
		var snapshot = await _repository.LoadAsync(cancellationToken)
			.ConfigureAwait(false);
		return Array.AsReadOnly(snapshot.MoneyOrders.ToArray());
	}

	public Task CreateAndProcessAsync(
		string staffId,
		decimal amount,
		string sender,
		string recipient,
		decimal fee,
		IdentityVerification? verification,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(staffId))
			throw new UnauthorizedAccessException(
				"An active Branch Manager is required to process a money order.");
		if (_verificationRule is null || _highValueThreshold is null)
			throw new InvalidOperationException(
				"Money-order high-value threshold is not configured.");

		return _repository.UpdateAsync(snapshot =>
		{
			var staff = snapshot.Staff.SingleOrDefault(
				person => string.Equals(
					person.Id, staffId, StringComparison.Ordinal));
			if (staff is not { IsActive: true, Role: StaffRole.BranchManager })
			{
				throw new UnauthorizedAccessException(
					"An active Branch Manager is required to process a money order.");
			}

			var moneyOrder = new MoneyOrder(
				new Money(amount, _highValueThreshold.Currency),
				sender,
				recipient,
				new Money(fee, _highValueThreshold.Currency),
				verification);
			_verificationRule.EnsureVerification(moneyOrder, verification);

			if (verification is { IsSuccessful: false })
				throw new InvalidOperationException(
					"Identity verification was not successful.");

			var updatedVerifications = verification is null
				? snapshot.IdentityVerifications
				: snapshot.IdentityVerifications.Append(verification);
			return new ApexPostSnapshot(
				snapshot.Branches,
				snapshot.Staff,
				snapshot.Products,
				snapshot.BranchStocks,
				snapshot.ShippingItems,
				snapshot.ShippingRates,
				snapshot.MoneyOrders.Append(moneyOrder),
				updatedVerifications,
				snapshot.TransactionBaskets);
		}, cancellationToken);
	}
}
