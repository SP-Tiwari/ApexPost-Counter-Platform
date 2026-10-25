using ApexPost.Domain.Reporting;

namespace ApexPost.Application.Services;

public sealed class ShippingVolumeReportApplicationService
{
	private const string DeniedMessage =
		"An active Auditor or Administrator is required to access shipping-volume reports.";

	private readonly IApexPostRepository _repository;
	private readonly ShippingVolumeReportService _reportService = new();

	public ShippingVolumeReportApplicationService(
		IApexPostRepository repository)
	{
		ArgumentNullException.ThrowIfNull(repository);
		_repository = repository;
	}

	public async Task<IReadOnlyList<ShippingVolumeReportRow>> GetReportAsync(
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

		return _reportService.CreateReport(snapshot.ShippingItems);
	}
}
