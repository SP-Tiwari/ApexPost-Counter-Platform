using ApexPost.Domain.Reporting;
using ApexPost.Domain.Specifications;

namespace ApexPost.Application.Services;

public sealed class LowStockReportApplicationService
{
	private readonly IApexPostRepository _repository;
	private readonly int? _threshold;
	private readonly LowStockReportService _reportService = new();

	public LowStockReportApplicationService(
		IApexPostRepository repository,
		int? threshold)
	{
		ArgumentNullException.ThrowIfNull(repository);
		if (threshold < 0)
			throw new ArgumentOutOfRangeException(
				nameof(threshold),
				"Low-stock threshold cannot be negative.");

		_repository = repository;
		_threshold = threshold;
	}

	public async Task<IReadOnlyList<LowStockReportRow>> GetReportAsync(
		string staffId,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(staffId))
			throw new UnauthorizedAccessException(
				"An active Branch Manager is required to access low-stock reports.");

		var snapshot = await _repository.LoadAsync(cancellationToken)
			.ConfigureAwait(false);
		var matches = snapshot.Staff
			.Where(staff => string.Equals(
				staff.Id, staffId, StringComparison.Ordinal))
			.Take(2)
			.ToArray();
		if (matches.Length != 1 ||
			!matches[0].IsActive ||
			matches[0].Role != StaffRole.BranchManager)
		{
			throw new UnauthorizedAccessException(
				"An active Branch Manager is required to access low-stock reports.");
		}

		if (_threshold is null)
			throw new InvalidOperationException(
				"Reports:LowStockThreshold is not configured.");

		var branchStocks = snapshot.BranchStocks
			.Matching(new StockAtBranchSpecification(matches[0].HomeBranchCode));
		return _reportService.CreateReport(branchStocks, _threshold.Value);
	}
}
