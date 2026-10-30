
namespace ApexPost.Web.Controllers;

// Money orders are issued as lines of a counter transaction
// (TransactionsController.Create). This controller lists branch records.
[Authorize(Policy = ApexPostPolicies.BranchManagerOnly)]
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
			order.IdentityVerification?.IsSuccessful,
			order.Status)).ToArray());
	}
}
