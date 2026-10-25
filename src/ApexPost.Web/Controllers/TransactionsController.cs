using ApexPost.Application.Services;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.Notifications;
using ApexPost.Domain.ValueObjects;
using ApexPost.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

[Authorize(Roles = nameof(StaffRole.BranchManager))]
[Route("[controller]")]
public sealed class TransactionsController : Controller
{
	private readonly TransactionApplicationService _transactions;
	private readonly MixedBasketApplicationService _mixedBaskets;

	public TransactionsController(
		TransactionApplicationService transactions,
		MixedBasketApplicationService mixedBaskets)
	{
		ArgumentNullException.ThrowIfNull(transactions);
		ArgumentNullException.ThrowIfNull(mixedBaskets);
		_transactions = transactions;
		_mixedBaskets = mixedBaskets;
	}

	[HttpGet]
	public async Task<IActionResult> Index(CancellationToken cancellationToken)
	{
		var staffId = User.FindFirst("StaffId")?.Value;
		if (string.IsNullOrWhiteSpace(staffId))
			return Forbid();

		IReadOnlyList<TransactionBasketSnapshot> transactions;
		try
		{
			transactions = await _transactions.GetTransactionBasketsAsync(
				staffId, cancellationToken);
		}
		catch (UnauthorizedAccessException)
		{
			return Forbid();
		}
		catch (KeyNotFoundException)
		{
			return NotFound();
		}
		catch (InvalidOperationException)
		{
			return Forbid();
		}
		var model = transactions
			.Select(basket => new TransactionListViewModel(
				basket.Id,
				basket.PaymentStatus,
				basket.Total.Amount,
				basket.Currency,
				basket.BranchCode?.Value))
			.ToArray();
		return View(model);
	}

	[HttpGet("Create")]
	public async Task<IActionResult> Create(
		CancellationToken cancellationToken)
	{
		var staffId = User.FindFirst("StaffId")?.Value;
		if (string.IsNullOrWhiteSpace(staffId))
			return Forbid();
		try
		{
			var model = await BuildCreateViewModelAsync(
				new CreateTransactionViewModel
				{
					TransactionId = Guid.NewGuid()
				},
				staffId,
				cancellationToken);
			return View(model);
		}
		catch (UnauthorizedAccessException)
		{
			return Forbid();
		}
	}

	[HttpPost("Create")]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> Create(
		CreateTransactionViewModel model,
		CancellationToken cancellationToken)
	{
		if (model.TransactionId == Guid.Empty)
			ModelState.AddModelError(
				nameof(model.TransactionId), "A transaction ID is required.");

		var hasGoods = !string.IsNullOrWhiteSpace(model.GoodsSku);
		if (hasGoods && model.GoodsQuantity is not > 0)
			ModelState.AddModelError(
				nameof(model.GoodsQuantity),
				"Goods quantity must be greater than zero.");
		if (!hasGoods && model.GoodsQuantity.HasValue)
			ModelState.AddModelError(
				nameof(model.GoodsSku),
				"Select a product when specifying a goods quantity.");

		ShippingBasketRequest? shippingRequest = null;
		if (model.IncludeShipping)
		{
			if (model.ShippingType is null ||
				!Enum.IsDefined(model.ShippingType.Value))
				ModelState.AddModelError(
					nameof(model.ShippingType), "Select a valid shipping type.");
			if (model.ServiceLevel is null ||
				!Enum.IsDefined(model.ServiceLevel.Value))
				ModelState.AddModelError(
					nameof(model.ServiceLevel), "Select a valid service level.");
			if (string.IsNullOrWhiteSpace(model.DestinationZone))
				ModelState.AddModelError(
					nameof(model.DestinationZone),
					"Select a destination zone.");
			if (model.WeightKilograms is not > 0m)
				ModelState.AddModelError(
					nameof(model.WeightKilograms),
					"Weight must be greater than zero.");
			if (model.LengthCm is not > 0m)
				ModelState.AddModelError(
					nameof(model.LengthCm),
					"Length must be greater than zero.");
			if (model.WidthCm is not > 0m)
				ModelState.AddModelError(
					nameof(model.WidthCm),
					"Width must be greater than zero.");
			if (model.HeightCm is not > 0m)
				ModelState.AddModelError(
					nameof(model.HeightCm),
					"Height must be greater than zero.");

			if (ModelState.IsValid)
			{
				shippingRequest = new ShippingBasketRequest(
					model.ShippingType!.Value,
					model.WeightKilograms!.Value,
					model.LengthCm!.Value,
					model.WidthCm!.Value,
					model.HeightCm!.Value,
					model.DestinationZone!,
					model.ServiceLevel!.Value);
			}
		}

		if (!hasGoods && !model.IncludeShipping)
			ModelState.AddModelError(
				string.Empty,
				"Add at least one goods item or shipping service.");

		var staffId = User.FindFirst("StaffId")?.Value;
		if (string.IsNullOrWhiteSpace(staffId))
			return Forbid();

		if (!_mixedBaskets.HasTaxRates)
			ModelState.AddModelError(
				string.Empty,
				"Configure transaction tax rates before processing transactions.");

		if (!ModelState.IsValid)
		{
			Response.StatusCode = StatusCodes.Status400BadRequest;
			return View(await BuildCreateViewModelAsync(
				model, staffId, cancellationToken));
		}

		try
		{
			var goods = hasGoods
				? new[]
				{
					new GoodsBasketRequest(
						model.GoodsSku!,
						model.GoodsQuantity!.Value)
				}
				: [];
			IReadOnlyList<ShippingBasketRequest> shipments = shippingRequest is null
				? []
				: [shippingRequest];
			var transactionId =
				await _mixedBaskets.CreateAndProcessAsync(
					model.TransactionId,
					staffId,
					goods,
					shipments,
					cancellationToken);
			TempData["TransactionActionStatus"] =
				"Transaction processed.";
			return RedirectToAction(
				nameof(Details), new { transactionId });
		}
		catch (KeyNotFoundException)
		{
			return NotFound();
		}
		catch (UnauthorizedAccessException)
		{
			return Forbid();
		}
		catch (ArgumentException)
		{
			ModelState.AddModelError(
				string.Empty,
				"One or more transaction values are invalid.");
			Response.StatusCode = StatusCodes.Status400BadRequest;
			return View(await BuildCreateViewModelAsync(
				model, staffId, cancellationToken));
		}
		catch (InvalidOperationException)
		{
			ModelState.AddModelError(
				string.Empty,
				"The transaction could not be processed. Check configured rates, weight limits, stock, and submission status.");
			Response.StatusCode = StatusCodes.Status409Conflict;
			return View(await BuildCreateViewModelAsync(
				model, staffId, cancellationToken));
		}
	}

	[HttpGet("{transactionId:guid}")]
	public async Task<IActionResult> Details(
		Guid transactionId,
		CancellationToken cancellationToken)
	{
		var staffId = User.FindFirst("StaffId")?.Value;
		if (string.IsNullOrWhiteSpace(staffId))
			return Forbid();

		try
		{
			return View(await BuildDetailsViewModelAsync(
				transactionId, staffId, cancellationToken));
		}
		catch (KeyNotFoundException)
		{
			return NotFound();
		}
		catch (UnauthorizedAccessException)
		{
			return Forbid();
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

		var staffId = User.FindFirst("StaffId")?.Value;
		if (string.IsNullOrWhiteSpace(staffId))
			return Forbid();

		if (!ModelState.IsValid)
		{
			return await RenderDetailsWithErrorAsync(
				transactionId,
				staffId,
				StatusCodes.Status400BadRequest,
				"Enter a reason of no more than 500 characters.",
				cancellationToken);
		}

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
		catch (UnauthorizedAccessException)
		{
			return Forbid();
		}
		catch (ArgumentException)
		{
			return await RenderDetailsWithErrorAsync(
				transactionId,
				staffId,
				StatusCodes.Status400BadRequest,
				"Enter a valid reason for this transaction action.",
				cancellationToken);
		}
		catch (InvalidOperationException)
		{
			return await RenderDetailsWithErrorAsync(
				transactionId,
				staffId,
				StatusCodes.Status409Conflict,
				"This transaction cannot be changed in its current state.",
				cancellationToken);
		}
	}

	private async Task<IActionResult> RenderDetailsWithErrorAsync(
		Guid transactionId,
		string staffId,
		int statusCode,
		string errorMessage,
		CancellationToken cancellationToken)
	{
		try
		{
			var model = await BuildDetailsViewModelAsync(
				transactionId, staffId, cancellationToken);
			ViewData["TransactionActionError"] = errorMessage;
			Response.StatusCode = statusCode;
			return View("Details", model);
		}
		catch (KeyNotFoundException)
		{
			return NotFound();
		}
		catch (UnauthorizedAccessException)
		{
			return Forbid();
		}
	}

	private async Task<TransactionDetailsViewModel> BuildDetailsViewModelAsync(
		Guid transactionId,
		string staffId,
		CancellationToken cancellationToken)
	{
		var basket = await _transactions.GetTransactionBasketAsync(
			transactionId,
			staffId,
			cancellationToken);
		return new TransactionDetailsViewModel(
			basket.Id,
			basket.PaymentStatus,
			basket.Subtotal.Amount,
			basket.Tax.Amount,
			basket.Total.Amount,
			basket.Currency,
			basket.BranchCode?.Value,
			basket.LineItems.Select(item => new TransactionLineViewModel(
				item.Description,
				item.Type,
				item.UnitPrice.Amount,
				item.Quantity,
				item.Subtotal.Amount,
				item.UnitPrice.Currency,
				item.ShippingItem is null
					? null
					: $"{item.ShippingItem.Type}; {item.ShippingItem.Weight.Kilograms} kg; " +
						$"{item.ShippingItem.Dimensions.LengthCm} x " +
						$"{item.ShippingItem.Dimensions.WidthCm} x " +
						$"{item.ShippingItem.Dimensions.HeightCm} cm; " +
						$"{item.ShippingItem.DestinationZone}; " +
						$"{item.ShippingItem.ServiceLevel}")).ToArray(),
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

	private async Task<CreateTransactionViewModel> BuildCreateViewModelAsync(
		CreateTransactionViewModel model,
		string staffId,
		CancellationToken cancellationToken)
	{
		var catalog = await _mixedBaskets.GetCatalogAsync(
			staffId, cancellationToken);
		model.Products = catalog.Products;
		model.Branch = catalog.Branch;
		model.ShippingRates = catalog.ShippingRates;
		model.CanSubmit = _mixedBaskets.HasTaxRates &&
			(catalog.Products.Count > 0 || catalog.ShippingRates.Count > 0);
		return model;
	}
}
