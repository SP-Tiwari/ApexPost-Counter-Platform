namespace ApexPost.Web.Controllers;

[Authorize(Roles = nameof(StaffRole.BranchManager))]
[Route("[controller]/[action]")]
public sealed class ReportsController(
	LowStockReportApplicationService lowStockReports) : Controller
{
	[HttpGet]
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
