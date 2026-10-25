using ApexPost.Domain.Reporting;

namespace ApexPost.Application.Services;

public sealed class DailyBranchSalesReportApplicationService
{
	private const string DeniedMessage =
		"An active Branch Manager is required to access daily sales reports.";

	private readonly IApexPostRepository _repository;
	private readonly DailySalesReportService _reportService = new();

	public DailyBranchSalesReportApplicationService(
		IApexPostRepository repository)
	{
		ArgumentNullException.ThrowIfNull(repository);
		_repository = repository;
	}

	public async Task<IReadOnlyList<DailySalesReportRow>> GetReportAsync(
		string staffId,
		DateOnly businessDate,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(staffId))
			throw new UnauthorizedAccessException(DeniedMessage);

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
			throw new UnauthorizedAccessException(DeniedMessage);
		}

		var branchCode = matches[0].HomeBranchCode;
		var entries = snapshot.TransactionBaskets
			.Where(basket =>
				basket.BranchCode == branchCode &&
				basket.PaymentStatus == TransactionPaymentStatus.Paid)
			.Select(basket => new DailySalesReportEntry(
				branchCode,
				businessDate,
				basket));

		return _reportService.CreateReport(entries);
	}
}