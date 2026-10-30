using ApexPost.Application.Caching;
using ApexPost.Application.Courier;
using ApexPost.Domain.Reporting;
using ApexPost.Domain.Specifications;

namespace ApexPost.Application.Services;

public sealed record DashboardSalesSummary(
	DateOnly BusinessDate,
	int TransactionCount,
	DailySalesReportRow? Totals);

// Sales, low-stock and shipment panels follow the existing report rules and
// are filled only for Branch Managers. LowStock is null when no threshold is
// configured; RecentShipments is null when no courier service is configured.
public sealed record BranchDashboard(
	string StaffName,
	StaffRole Role,
	Branch Branch,
	int ActiveCounterCount,
	int ProductCount,
	int ShippingZoneCount,
	DashboardSalesSummary? TodaySales,
	IReadOnlyList<LowStockReportRow>? LowStock,
	IReadOnlyList<CourierTrackingResult>? RecentShipments,
	DateTimeOffset GeneratedAtUtc);

public sealed class BranchDashboardApplicationService
{
	// How many of today's shipments the dashboard looks up; a display limit.
	private const int RecentShipmentLimit = 5;

	private readonly IApexPostRepository _repository;
	private readonly ReferenceDataCache _referenceData;
	private readonly TimeProvider _timeProvider;
	private readonly int? _lowStockThreshold;
	private readonly ICourierTrackingService? _courier;
	private readonly DailySalesReportService _salesReport = new();
	private readonly LowStockReportService _lowStockReport = new();

	public BranchDashboardApplicationService(
		IApexPostRepository repository,
		ReferenceDataCache referenceData,
		TimeProvider timeProvider,
		int? lowStockThreshold = null,
		ICourierTrackingService? courier = null)
	{
		ArgumentNullException.ThrowIfNull(repository);
		ArgumentNullException.ThrowIfNull(referenceData);
		ArgumentNullException.ThrowIfNull(timeProvider);
		if (lowStockThreshold < 0)
			throw new ArgumentOutOfRangeException(
				nameof(lowStockThreshold), "Low-stock threshold cannot be negative.");

		_repository = repository;
		_referenceData = referenceData;
		_timeProvider = timeProvider;
		_lowStockThreshold = lowStockThreshold;
		_courier = courier;
	}

	public async Task<BranchDashboard> GetDashboardAsync(
		string staffId,
		CancellationToken cancellationToken = default)
	{
		var snapshot = await _repository.LoadAsync(cancellationToken)
			.ConfigureAwait(false);
		var staff = FindActiveStaff(snapshot, staffId);
		var branch = snapshot.Branches.SingleOrDefault(
			candidate => candidate.Code == staff.HomeBranchCode)
			?? throw new KeyNotFoundException(
				$"Branch '{staff.HomeBranchCode}' was not found.");

		var generatedAtUtc = _timeProvider.GetUtcNow();
		var today = DateOnly.FromDateTime(generatedAtUtc.UtcDateTime);
		var isManager = staff.Role == StaffRole.BranchManager;
		TransactionBasket[] todaysSales = isManager
			? snapshot.TransactionBaskets
				.Matching(new PaidTransactionSpecification()
					.And(new TransactionAtBranchSpecification(branch.Code))
					.And(new TransactionOnBusinessDateSpecification(today)))
				.ToArray()
			: [];

		// The catalogue (from the cache) and the courier lookups are
		// independent I/O, so they run in parallel.
		var referenceTask = _referenceData.GetAsync(cancellationToken).AsTask();
		var shipmentsTask = isManager && _courier is not null
			? LookUpRecentShipmentsAsync(todaysSales, _courier, cancellationToken)
			: Task.FromResult<IReadOnlyList<CourierTrackingResult>?>(null);
		await Task.WhenAll(referenceTask, shipmentsTask).ConfigureAwait(false);
		var referenceData = await referenceTask.ConfigureAwait(false);

		return new BranchDashboard(
			staff.Name,
			staff.Role,
			branch,
			branch.CounterTerminals.Count(terminal => terminal.IsActive),
			referenceData.Products.Count,
			referenceData.ShippingRates
				.Select(rate => rate.DestinationZone)
				.Distinct(StringComparer.Ordinal)
				.Count(),
			isManager ? SummarizeSales(branch.Code, today, todaysSales) : null,
			isManager && _lowStockThreshold is { } threshold
				? _lowStockReport.CreateReport(
					snapshot.BranchStocks.Matching(
						new StockAtBranchSpecification(branch.Code)),
					threshold)
				: null,
			await shipmentsTask.ConfigureAwait(false),
			generatedAtUtc);
	}

	private DashboardSalesSummary SummarizeSales(
		BranchCode branchCode,
		DateOnly today,
		IReadOnlyCollection<TransactionBasket> todaysSales)
	{
		var rows = _salesReport.CreateReport(todaysSales.Select(basket =>
			new DailySalesReportEntry(branchCode, today, basket)));
		return new DashboardSalesSummary(
			today, todaysSales.Count, rows.SingleOrDefault());
	}

	private static async Task<IReadOnlyList<CourierTrackingResult>?>
		LookUpRecentShipmentsAsync(
			IEnumerable<TransactionBasket> todaysSales,
			ICourierTrackingService courier,
			CancellationToken cancellationToken)
	{
		var shipmentIds = todaysSales
			.OrderByDescending(basket => basket.OccurredAtUtc)
			.SelectMany(basket => basket.LineItems)
			.Select(lineItem => lineItem.ShippingItem?.Id)
			.OfType<string>()
			.Take(RecentShipmentLimit)
			.ToArray();

		// The courier service bounds its own concurrency and times out each
		// lookup, so all lookups can be started together.
		return await Task.WhenAll(shipmentIds.Select(id =>
			courier.GetTrackingAsync(id, cancellationToken)))
			.ConfigureAwait(false);
	}

	private static Staff FindActiveStaff(ApexPostSnapshot snapshot, string staffId)
	{
		if (string.IsNullOrWhiteSpace(staffId))
			throw new UnauthorizedAccessException(
				"Active staff are required to view the dashboard.");

		var matches = snapshot.Staff
			.Where(staff => string.Equals(
				staff.Id, staffId, StringComparison.Ordinal))
			.Take(2)
			.ToArray();
		if (matches.Length != 1 || !matches[0].IsActive)
			throw new UnauthorizedAccessException(
				"Active staff are required to view the dashboard.");

		return matches[0];
	}
}
