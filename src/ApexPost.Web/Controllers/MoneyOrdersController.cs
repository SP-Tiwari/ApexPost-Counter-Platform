
namespace ApexPost.Web.Controllers;

[Authorize(Roles = nameof(ApexPost.Domain.Enums.StaffRole.BranchManager))]
[Route("[controller]/[action]")]
public sealed class MoneyOrdersController(
	MoneyOrderApplicationService moneyOrders) : Controller
{
	[HttpGet]
	public async Task<IActionResult> Index(
		CancellationToken cancellationToken)
	{
		if (!moneyOrders.IsConfigured)
			return StatusCode(
				StatusCodes.Status503ServiceUnavailable,
				"Money-order high-value threshold is not configured.");

		var staffId = User.FindFirst("StaffId")?.Value;
		if (string.IsNullOrWhiteSpace(staffId))
			return Forbid();

		IReadOnlyList<MoneyOrder> orders;
		try
		{
			orders = await moneyOrders.GetMoneyOrdersAsync(
				staffId, cancellationToken);
		}
		catch (UnauthorizedAccessException)
		{
			return Forbid();
		}

		return View(orders.Select(order => new MoneyOrderListItemViewModel(
			order.Amount.Amount,
			order.Amount.Currency,
			order.Sender,
			order.Recipient,
			order.Fee.Amount,
			order.IdentityVerification?.DocumentType,
			order.IdentityVerification?.MaskedDocumentIdentifier,
			order.IdentityVerification?.IsSuccessful)).ToArray());
	}

	[HttpGet]
	public IActionResult Create()
	{
		if (!moneyOrders.IsConfigured)
			return StatusCode(
				StatusCodes.Status503ServiceUnavailable,
				"Money-order high-value threshold is not configured.");

		ViewData["MoneyOrderCurrency"] = moneyOrders.Currency;
		return View(new MoneyOrderCreateViewModel());
	}

	[HttpPost]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> Create(
		MoneyOrderCreateViewModel model,
		CancellationToken cancellationToken)
	{
		if (!moneyOrders.IsConfigured || moneyOrders.Currency is null)
			return StatusCode(
				StatusCodes.Status503ServiceUnavailable,
				"Money-order high-value threshold is not configured.");

		if (model.DocumentCheckedInPerson &&
			(string.IsNullOrWhiteSpace(model.DocumentType) ||
				string.IsNullOrWhiteSpace(model.DocumentIdentifier)))
		{
			ModelState.AddModelError(
				string.Empty,
				"Document type and identifier are required when recording an in-person check.");
		}
		else if (!model.DocumentCheckedInPerson &&
			(!string.IsNullOrWhiteSpace(model.DocumentType) ||
				!string.IsNullOrWhiteSpace(model.DocumentIdentifier) ||
				model.IdentityCheckPassed))
		{
			ModelState.AddModelError(
				string.Empty,
				"Confirm an in-person document check before entering verification details.");
		}

		if (!ModelState.IsValid)
			return RenderInvalidInput(model);

		IdentityVerification? verification = null;
		if (model.DocumentCheckedInPerson)
		{
			try
			{
				verification = new IdentityVerification(
					model.DocumentType!,
					model.DocumentIdentifier!,
					model.IdentityCheckPassed);
			}
			catch (ArgumentException)
			{
				ModelState.AddModelError(
					string.Empty,
					"Enter valid identity document details.");
				return RenderInvalidInput(model);
			}
		}

		try
		{
			await moneyOrders.CreateAndProcessAsync(
				User.FindFirst("StaffId")?.Value ?? string.Empty,
				model.Amount,
				model.Sender,
				model.Recipient,
				model.Fee,
				verification,
				cancellationToken);
		}
		catch (UnauthorizedAccessException)
		{
			return Forbid();
		}
		catch (ArgumentException)
		{
			ModelState.AddModelError(
				string.Empty,
				"Enter valid money-order amount, fee, sender, and recipient details.");
			return RenderInvalidInput(model);
		}
		catch (InvalidOperationException)
		{
			ModelState.AddModelError(
				string.Empty,
				"Identity verification did not succeed or is required for this amount.");
			return RenderInvalidInput(model);
		}

		TempData["MoneyOrderStatus"] = "Money order processed.";
		return RedirectToAction(nameof(Index));
	}

	private IActionResult RenderInvalidInput(
		MoneyOrderCreateViewModel model)
	{
		model.DocumentIdentifier = string.Empty;
		ModelState.Remove(nameof(model.DocumentIdentifier));
		ViewData["MoneyOrderCurrency"] = moneyOrders.Currency;
		Response.StatusCode = StatusCodes.Status400BadRequest;
		return View("Create", model);
	}
}
