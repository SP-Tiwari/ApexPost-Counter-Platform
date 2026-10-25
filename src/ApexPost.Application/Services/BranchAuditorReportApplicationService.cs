using ApexPost.Domain.Reporting;

namespace ApexPost.Application.Services;

public sealed class BranchAuditorReportApplicationService
{
	private const string DeniedMessage =
		"An active Auditor or Administrator is required to access branch auditor reports.";

	private readonly IApexPostRepository _repository;
	private readonly BranchAuditorReportService _reportService = new();

	public BranchAuditorReportApplicationService(
		IApexPostRepository repository)
	{
		ArgumentNullException.ThrowIfNull(repository);
		_repository = repository;
	}

	public async Task<IReadOnlyList<BranchAuditorReportRow>> GetReportAsync(
		string staffId,
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
			matches[0].Role is not (StaffRole.Auditor or StaffRole.Administrator))
		{
			throw new UnauthorizedAccessException(DeniedMessage);
		}

		var entries = snapshot.MoneyOrders
			.Where(order => order.ProcessingBranchCode is not null)
			.Select(order => new BranchAuditorReportEntry(
				order.ProcessingBranchCode!,
				order,
				order.IdentityVerification));

		return _reportService.CreateReport(entries);
	}
}