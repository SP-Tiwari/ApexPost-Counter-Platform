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
			var basket = await _transactions.GetTransactionBasketAsync(
				transactionId,
				cancellationToken);
			var model = new TransactionDetailsViewModel(
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
			return View(model);
		}
		catch (KeyNotFoundException)
		{
			return NotFound();
		}
	}
}
