using ApexPost.Application.Persistence;

namespace ApexPost.Web.Controllers;

[Authorize(Roles = nameof(StaffRole.Administrator))]
[Route("[controller]/[action]")]
public sealed class AdministrationController(AdministrationApplicationService administration) : Controller
{
	[HttpGet]
	public async Task<IActionResult> Index(CancellationToken cancellationToken)
	{
		try { return View(ToModel(await administration.GetSnapshotAsync(StaffId(), cancellationToken))); }
		catch (UnauthorizedAccessException) { return Forbid(); }
	}

	[HttpPost, ValidateAntiForgeryToken]
	public async Task<IActionResult> CreateStaff(CreateStaffViewModel model, CancellationToken cancellationToken)
	{
		if (!ModelState.IsValid) return await BadFormAsync(cancellationToken);
		try { await administration.AddStaffAsync(StaffId(), model.Id!, model.Name!, model.Email!, model.Role!.Value, model.HomeBranchCode!, cancellationToken); return RedirectToAction(nameof(Index)); }
		catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException) { ModelState.AddModelError(string.Empty, exception.Message); return await BadFormAsync(cancellationToken); }
	}

	[HttpPost, ValidateAntiForgeryToken]
	public async Task<IActionResult> CreateBranch(CreateBranchViewModel model, CancellationToken cancellationToken)
	{
		if (!ModelState.IsValid) return await BadFormAsync(cancellationToken);
		try { await administration.AddBranchAsync(StaffId(), model.Code!, model.Name!, model.Region!, model.OpeningHours!, model.TerminalId!, model.TerminalName!, cancellationToken); return RedirectToAction(nameof(Index)); }
		catch (Exception exception) when (exception is ArgumentException or InvalidOperationException) { ModelState.AddModelError(string.Empty, exception.Message); return await BadFormAsync(cancellationToken); }
	}

	[HttpPost, ValidateAntiForgeryToken]
	public async Task<IActionResult> CreateProduct(CreateProductViewModel model, CancellationToken cancellationToken)
	{
		if (!ModelState.IsValid || model.UnitPrice is null) return await BadFormAsync(cancellationToken);
		try { await administration.AddProductAsync(StaffId(), model.Sku!, model.Name!, model.Category!.Value, model.UnitPrice.Value, model.Currency!, model.TaxCategory!.Value, cancellationToken); return RedirectToAction(nameof(Index)); }
		catch (Exception exception) when (exception is ArgumentException or InvalidOperationException) { ModelState.AddModelError(string.Empty, exception.Message); return await BadFormAsync(cancellationToken); }
	}

	[HttpPost, ValidateAntiForgeryToken]
	public async Task<IActionResult> SetBranchStatus(string code, bool isActive, CancellationToken cancellationToken)
	{
		try { await administration.SetBranchStatusAsync(StaffId(), code, isActive, cancellationToken); return RedirectToAction(nameof(Index)); }
		catch (Exception exception) when (exception is ArgumentException or KeyNotFoundException) { ModelState.AddModelError(string.Empty, exception.Message); return await BadFormAsync(cancellationToken); }
	}

	[HttpPost, ValidateAntiForgeryToken]
	public async Task<IActionResult> UpdateStaff(
		string staffId, StaffRole role, string homeBranchCode, bool isActive,
		CancellationToken cancellationToken)
	{
		try
		{
			await administration.UpdateStaffAsync(
				StaffId(), staffId, role, homeBranchCode, isActive, cancellationToken);
			return RedirectToAction(nameof(Index));
		}
		catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException)
		{
			ModelState.AddModelError(string.Empty, exception.Message);
			return await BadFormAsync(cancellationToken);
		}
	}

	private string StaffId() => User.FindFirst("StaffId")?.Value ?? throw new UnauthorizedAccessException();
	private async Task<IActionResult> BadFormAsync(CancellationToken cancellationToken)
	{
		Response.StatusCode = StatusCodes.Status400BadRequest;
		return View(nameof(Index), ToModel(await administration.GetSnapshotAsync(StaffId(), cancellationToken)));
	}
	private static AdministrationViewModel ToModel(ApexPostSnapshot snapshot) => new() { Branches = snapshot.Branches, Staff = snapshot.Staff, Products = snapshot.Products };
}
