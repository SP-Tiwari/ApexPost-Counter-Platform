
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
		string staffId,
		CancellationToken cancellationToken = default)
	{
		var snapshot = await _repository.LoadAsync(cancellationToken)
			.ConfigureAwait(false);
		var staff = FindActiveBranchManager(snapshot, staffId);
		return Array.AsReadOnly(snapshot.MoneyOrders
			.Where(order =>
				order.ProcessingBranchCode == staff.HomeBranchCode)
			.ToArray());
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
			var staff = FindActiveBranchManager(snapshot, staffId);

			var moneyOrder = new MoneyOrder(
				new Money(amount, _highValueThreshold.Currency),
				sender,
				recipient,
				new Money(fee, _highValueThreshold.Currency),
				verification,
				staff.HomeBranchCode);
			_verificationRule.EnsureVerification(moneyOrder, verification);

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

	private static Staff FindActiveBranchManager(
		ApexPostSnapshot snapshot,
		string staffId)
	{
		if (string.IsNullOrWhiteSpace(staffId))
			throw new UnauthorizedAccessException(
				"An active Branch Manager is required to access money orders.");

		var matches = snapshot.Staff
			.Where(person => string.Equals(
				person.Id, staffId, StringComparison.Ordinal))
			.Take(2)
			.ToArray();
		if (matches.Length != 1 ||
			!matches[0].IsActive ||
			matches[0].Role != StaffRole.BranchManager)
		{
			throw new UnauthorizedAccessException(
				"An active Branch Manager is required to access money orders.");
		}

		return matches[0];
	}
}
