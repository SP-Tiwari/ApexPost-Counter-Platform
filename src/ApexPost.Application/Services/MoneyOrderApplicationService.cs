
namespace ApexPost.Application.Services;

// Reads the branch's money-order records for the list page. Money orders are
// issued only as basket lines (MixedBasketApplicationService).
public sealed class MoneyOrderApplicationService
{
	private readonly IApexPostRepository _repository;
	private readonly Money? _highValueThreshold;

	public MoneyOrderApplicationService(
		IApexPostRepository repository,
		Money? highValueThreshold)
	{
		ArgumentNullException.ThrowIfNull(repository);
		_repository = repository;
		_highValueThreshold = highValueThreshold;
	}

	public bool IsConfigured => _highValueThreshold is not null;

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
