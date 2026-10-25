namespace ApexPost.Web.Controllers;

[Authorize]
[Route("[controller]/[action]")]
public sealed class ReportsController(
	LowStockReportApplicationService lowStockReports,
	ShippingVolumeReportApplicationService shippingVolumeReports) : Controller
{
	[HttpGet]
	[Authorize(Roles = nameof(StaffRole.Auditor) + "," +
		nameof(StaffRole.Administrator))]
	public async Task<IActionResult> ShippingVolume(
		CancellationToken cancellationToken)
	{
		var staffId = User.FindFirst("StaffId")?.Value;
		if (string.IsNullOrWhiteSpace(staffId))
			return Forbid();

		try
		{
			var report = await shippingVolumeReports.GetReportAsync(
				staffId, cancellationToken);
			return View(report);
		}
		catch (UnauthorizedAccessException)
		{
			return Forbid();
		}
	}

	[HttpGet]
	[Authorize(Roles = nameof(StaffRole.BranchManager))]
	public async Task<IActionResult> LowStock(
		CancellationToken cancellationToken)
	{
		var staffId = User.FindFirst("StaffId")?.Value;
		if (string.IsNullOrWhiteSpace(staffId))
			return Forbid();

		try
		{
			var report = await lowStockReports.GetReportAsync(
				staffId, cancellationToken);
			return View(report);
		}
		catch (UnauthorizedAccessException)
		{
			return Forbid();
		}
		catch (InvalidOperationException)
		{
			return StatusCode(
				StatusCodes.Status503ServiceUnavailable,
				"Low-stock reporting is unavailable because its threshold is not configured.");
		}
	}
}
