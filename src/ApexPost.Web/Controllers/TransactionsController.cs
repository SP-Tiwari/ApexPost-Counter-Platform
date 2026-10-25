using ApexPost.Application.Services;
using ApexPost.Domain.Enums;
using ApexPost.Domain.Notifications;
using ApexPost.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

[Authorize(Roles = nameof(StaffRole.BranchManager))]
[Route("[controller]")]
public sealed class TransactionsController : Controller
{
	private readonly TransactionApplicationService _transactions;

	public TransactionsController(TransactionApplicationService transactions)
	{
		ArgumentNullException.ThrowIfNull(transactions);
		_transactions = transactions;
	}

	[HttpGet]
	public async Task<IActionResult> Index(CancellationToken cancellationToken)
	{
		var transactions = await _transactions.GetTransactionBasketsAsync(
			cancellationToken);
		var model = transactions
			.Select(basket => new TransactionListViewModel(
				basket.Id,
				basket.PaymentStatus,
				basket.Total.Amount,
				basket.Currency))
			.ToArray();
		return View(model);
	}

	[HttpGet("{transactionId:guid}")]
	public async Task<IActionResult> Details(
		Guid transactionId,
		CancellationToken cancellationToken)
	{
		try
		{
			return View(await BuildDetailsViewModelAsync(
				transactionId, cancellationToken));
		}
		catch (KeyNotFoundException)
		{
			return NotFound();
		}
	}

	[HttpPost("{transactionId:guid}/void")]
	[ValidateAntiForgeryToken]
	public Task<IActionResult> Void(
		Guid transactionId,
		TransactionActionViewModel model,
		CancellationToken cancellationToken) =>
		ChangeStatusAsync(
			transactionId,
			model,
			"Transaction voided.",
			_transactions.VoidAsync,
			cancellationToken);

	[HttpPost("{transactionId:guid}/refund")]
	[ValidateAntiForgeryToken]
	public Task<IActionResult> Refund(
		Guid transactionId,
		TransactionActionViewModel model,
		CancellationToken cancellationToken) =>
		ChangeStatusAsync(
			transactionId,
			model,
			"Transaction refunded.",
			_transactions.RefundAsync,
			cancellationToken);

	private async Task<IActionResult> ChangeStatusAsync(
		Guid transactionId,
		TransactionActionViewModel model,
		string successMessage,
		Func<Guid, string, string, CancellationToken, Task> changeStatus,
		CancellationToken cancellationToken)
	{
		if (transactionId == Guid.Empty)
			return BadRequest();

		if (!ModelState.IsValid)
		{
			return await RenderDetailsWithErrorAsync(
				transactionId,
				StatusCodes.Status400BadRequest,
				"Enter a reason of no more than 500 characters.",
				cancellationToken);
		}

		var staffId = User.FindFirst("StaffId")?.Value;
		if (string.IsNullOrWhiteSpace(staffId))
			return Forbid();

		try
		{
			await changeStatus(
				transactionId,
				staffId,
				model.Reason,
				cancellationToken);
			TempData["TransactionActionStatus"] = successMessage;
			return RedirectToAction(
				nameof(Details), new { transactionId });
		}
		catch (KeyNotFoundException)
		{
			return NotFound();
		}
		catch (ArgumentException)
		{
			return await RenderDetailsWithErrorAsync(
				transactionId,
				StatusCodes.Status400BadRequest,
				"Enter a valid reason for this transaction action.",
				cancellationToken);
		}
		catch (InvalidOperationException)
		{
			return await RenderDetailsWithErrorAsync(
				transactionId,
				StatusCodes.Status409Conflict,
				"This transaction cannot be changed in its current state.",
				cancellationToken);
		}
	}

	private async Task<IActionResult> RenderDetailsWithErrorAsync(
		Guid transactionId,
		int statusCode,
		string errorMessage,
		CancellationToken cancellationToken)
	{
		try
		{
			var model = await BuildDetailsViewModelAsync(
				transactionId, cancellationToken);
			ViewData["TransactionActionError"] = errorMessage;
			Response.StatusCode = statusCode;
			return View("Details", model);
		}
		catch (KeyNotFoundException)
		{
			return NotFound();
		}
	}

	private async Task<TransactionDetailsViewModel> BuildDetailsViewModelAsync(
		Guid transactionId,
		CancellationToken cancellationToken)
	{
		var basket = await _transactions.GetTransactionBasketAsync(
			transactionId,
			cancellationToken);
		return new TransactionDetailsViewModel(
			basket.Id,
			basket.PaymentStatus,
			basket.Subtotal.Amount,
			basket.Tax.Amount,
			basket.Total.Amount,
			basket.Currency,
			basket.LineItems.Select(item => new TransactionLineViewModel(
				item.Description,
				item.Type,
				item.UnitPrice.Amount,
				item.Quantity,
				item.Subtotal.Amount,
				item.UnitPrice.Currency)).ToArray(),
			basket.Notifications.Select(notification =>
				new TransactionNotificationViewModel(
					notification switch
					{
						TransactionVoidedNotification => "Voided",
						TransactionRefundedNotification => "Refunded",
						_ => notification.GetType().Name
					},
					notification switch
					{
						TransactionVoidedNotification voided => voided.Reason,
						TransactionRefundedNotification refunded => refunded.Reason,
						_ => string.Empty
					})).ToArray());
	}
}
