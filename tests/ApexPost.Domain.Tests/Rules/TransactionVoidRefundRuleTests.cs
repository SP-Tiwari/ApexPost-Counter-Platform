
namespace ApexPost.Domain.Tests.Rules;

public class TransactionVoidRefundRuleTests
{
	[Fact]
	public void Void_WhenActiveManagerProvidesReason_VoidsOpenBasket()
	{
		var basket = CreateBasket();

		CreateRule().Void(CreateStaff(StaffRole.BranchManager), basket, "Duplicate entry", []);

		Assert.Equal(TransactionPaymentStatus.Voided, basket.PaymentStatus);
		Assert.Equal(
			"Duplicate entry",
			Assert.IsType<TransactionVoidedNotification>(
				Assert.Single(basket.Notifications)).Reason);
	}

	[Fact]
	public void Refund_WhenActiveManagerProvidesReason_RefundsPaidBasket()
	{
		var basket = CreateBasket();
		basket.Pay();

		CreateRule().Refund(CreateStaff(StaffRole.BranchManager), basket, "Customer request", []);

		Assert.Equal(TransactionPaymentStatus.Refunded, basket.PaymentStatus);
		Assert.Equal(
			"Customer request",
			Assert.IsType<TransactionRefundedNotification>(
				Assert.Single(basket.Notifications)).Reason);
	}

	[Theory]
	[InlineData(StaffRole.CounterClerk)]
	[InlineData(StaffRole.Auditor)]
	[InlineData(StaffRole.Administrator)]
	public void Void_WhenStaffIsNotManager_RejectsWithoutChangingStatus(StaffRole role)
	{
		var basket = CreateBasket();

		Assert.Throws<InvalidOperationException>(() =>
			CreateRule().Void(CreateStaff(role), basket, "Valid reason", []));

		Assert.Equal(TransactionPaymentStatus.Open, basket.PaymentStatus);
		Assert.Empty(basket.Notifications);
	}

	[Theory]
	[InlineData(StaffRole.CounterClerk)]
	[InlineData(StaffRole.Auditor)]
	[InlineData(StaffRole.Administrator)]
	public void Refund_WhenStaffIsNotManager_RejectsWithoutChangingStatus(StaffRole role)
	{
		var basket = CreateBasket();
		basket.Pay();

		Assert.Throws<InvalidOperationException>(() =>
			CreateRule().Refund(CreateStaff(role), basket, "Valid reason", []));

		Assert.Equal(TransactionPaymentStatus.Paid, basket.PaymentStatus);
		Assert.Empty(basket.Notifications);
	}

	[Fact]
	public void Void_WhenManagerIsInactive_RejectsWithoutChangingStatus()
	{
		var manager = CreateStaff(StaffRole.BranchManager);
		manager.Deactivate();
		var basket = CreateBasket();

		Assert.Throws<InvalidOperationException>(() =>
			CreateRule().Void(manager, basket, "Valid reason", []));

		Assert.Equal(TransactionPaymentStatus.Open, basket.PaymentStatus);
		Assert.Empty(basket.Notifications);
	}

	[Theory]
	[InlineData("")]
	[InlineData(" ")]
	public void Void_WhenReasonIsMissing_RejectsWithoutChangingStatus(string reason)
	{
		var basket = CreateBasket();

		Assert.Throws<ArgumentException>(() =>
			CreateRule().Void(CreateStaff(StaffRole.BranchManager), basket, reason, []));

		Assert.Equal(TransactionPaymentStatus.Open, basket.PaymentStatus);
		Assert.Empty(basket.Notifications);
	}

	[Theory]
	[InlineData("")]
	[InlineData(" ")]
	public void Refund_WhenReasonIsMissing_RejectsWithoutChangingStatus(string reason)
	{
		var basket = CreateBasket();
		basket.Pay();

		Assert.Throws<ArgumentException>(() =>
			CreateRule().Refund(CreateStaff(StaffRole.BranchManager), basket, reason, []));

		Assert.Equal(TransactionPaymentStatus.Paid, basket.PaymentStatus);
		Assert.Empty(basket.Notifications);
	}

	[Fact]
	public void Void_WhenBasketIsPaid_VoidsAndReturnsGoodsStock()
	{
		var stock = CreateStock("BOX-01", 5);
		var basket = CreateSoldBasket(stock, quantity: 3);

		CreateRule().Void(
			CreateStaff(StaffRole.BranchManager), basket, "Sale cancelled", [stock]);

		Assert.Equal(TransactionPaymentStatus.Voided, basket.PaymentStatus);
		Assert.Equal(5, stock.Quantity);
		Assert.Equal(
			"Sale cancelled",
			Assert.IsType<TransactionVoidedNotification>(
				Assert.Single(basket.Notifications)).Reason);
	}

	[Fact]
	public void Refund_WhenBasketIsPaid_ReturnsGoodsStock()
	{
		var stock = CreateStock("BOX-01", 5);
		var basket = CreateSoldBasket(stock, quantity: 3);

		CreateRule().Refund(
			CreateStaff(StaffRole.BranchManager), basket, "Customer request", [stock]);

		Assert.Equal(TransactionPaymentStatus.Refunded, basket.PaymentStatus);
		Assert.Equal(5, stock.Quantity);
	}

	[Fact]
	public void Void_WhenBasketIsOpen_DoesNotChangeStock()
	{
		var stock = CreateStock("BOX-01", 5);
		var basket = CreateBasket();
		basket.AddLineItem(CreateGoodsLine("BOX-01", 2));

		CreateRule().Void(
			CreateStaff(StaffRole.BranchManager), basket, "Entered in error", [stock]);

		Assert.Equal(TransactionPaymentStatus.Voided, basket.PaymentStatus);
		Assert.Equal(5, stock.Quantity);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void RepeatedReversal_ReturnsStockOnlyOnce(bool voidFirst)
	{
		var stock = CreateStock("BOX-01", 5);
		var basket = CreateSoldBasket(stock, quantity: 3);
		var manager = CreateStaff(StaffRole.BranchManager);
		if (voidFirst)
			CreateRule().Void(manager, basket, "First", [stock]);
		else
			CreateRule().Refund(manager, basket, "First", [stock]);

		Assert.Throws<InvalidOperationException>(() =>
			CreateRule().Void(manager, basket, "Second", [stock]));
		Assert.Throws<InvalidOperationException>(() =>
			CreateRule().Refund(manager, basket, "Second", [stock]));

		Assert.Equal(5, stock.Quantity);
		Assert.Single(basket.Notifications);
	}

	[Fact]
	public void Reversal_ReturnsStockOnlyForGoodsAtTheTransactionBranch()
	{
		var stock = CreateStock("BOX-01", 5);
		var otherBranchStock = new BranchStock(
			new BranchCode("MUM01"), new Sku("BOX-01"), 7);
		var basket = CreateEmptyBasket();
		basket.AddLineItem(CreateGoodsLine("BOX-01", 1));
		basket.AddLineItem(CreateGoodsLine("BOX-01", 2));
		new SaleProcessingService().ProcessSale(
			basket, new BranchCode("DEL01"), [stock]);

		CreateRule().Refund(
			CreateStaff(StaffRole.BranchManager),
			basket,
			"Customer request",
			[otherBranchStock, stock]);

		Assert.Equal(5, stock.Quantity);
		Assert.Equal(7, otherBranchStock.Quantity);
	}

	[Fact]
	public void Reversal_WhenStockRecordIsMissing_RejectsWithoutChanges()
	{
		var stock = CreateStock("BOX-01", 5);
		var basket = CreateSoldBasket(stock, quantity: 3);

		Assert.Throws<InvalidOperationException>(() =>
			CreateRule().Void(
				CreateStaff(StaffRole.BranchManager), basket, "Sale cancelled", []));

		Assert.Equal(TransactionPaymentStatus.Paid, basket.PaymentStatus);
		Assert.Equal(2, stock.Quantity);
		Assert.Empty(basket.Notifications);
	}

	[Fact]
	public void Reversal_WhenReasonIsMissing_DoesNotReturnStock()
	{
		var stock = CreateStock("BOX-01", 5);
		var basket = CreateSoldBasket(stock, quantity: 3);

		Assert.Throws<ArgumentException>(() =>
			CreateRule().Refund(
				CreateStaff(StaffRole.BranchManager), basket, " ", [stock]));

		Assert.Equal(TransactionPaymentStatus.Paid, basket.PaymentStatus);
		Assert.Equal(2, stock.Quantity);
	}

	[Fact]
	public void Reversal_WhenStaffIsNotManager_DoesNotReturnStock()
	{
		var stock = CreateStock("BOX-01", 5);
		var basket = CreateSoldBasket(stock, quantity: 3);

		Assert.Throws<InvalidOperationException>(() =>
			CreateRule().Void(
				CreateStaff(StaffRole.CounterClerk), basket, "Valid reason", [stock]));

		Assert.Equal(TransactionPaymentStatus.Paid, basket.PaymentStatus);
		Assert.Equal(2, stock.Quantity);
	}

	[Fact]
	public async Task ConcurrentVoidAndRefundOfPaidSale_ReturnStockOnce()
	{
		var stock = CreateStock("BOX-01", 10);
		var basket = CreateSoldBasket(stock, quantity: 4);
		var rule = CreateRule();
		var attempts = Enumerable.Range(0, 12)
			.Select(index => Task.Run(() =>
			{
				try
				{
					var manager = CreateStaff(StaffRole.BranchManager);
					if (index % 2 == 0)
						rule.Void(manager, basket, $"Reason {index}", [stock]);
					else
						rule.Refund(manager, basket, $"Reason {index}", [stock]);
					return true;
				}
				catch (InvalidOperationException)
				{
					return false;
				}
			}))
			.ToArray();

		var succeeded = await Task.WhenAll(attempts);

		Assert.Equal(1, succeeded.Count(result => result));
		Assert.Equal(10, stock.Quantity);
		Assert.Single(basket.Notifications);
	}

	[Fact]
	public void Refund_WhenBasketIsOpen_RejectsUsingExistingBasketStateRule()
	{
		var basket = CreateBasket();

		Assert.Throws<InvalidOperationException>(() =>
			CreateRule().Refund(CreateStaff(StaffRole.BranchManager), basket, "Valid reason", []));

		Assert.Equal(TransactionPaymentStatus.Open, basket.PaymentStatus);
		Assert.Empty(basket.Notifications);
	}

	[Fact]
	public void Refund_WhenBasketIsAlreadyRefunded_RejectsWithoutChangingStatus()
	{
		var basket = CreateBasket();
		basket.Pay();
		CreateRule().Refund(
			CreateStaff(StaffRole.BranchManager),
			basket,
			"Initial refund", []);

		Assert.Throws<InvalidOperationException>(() =>
			CreateRule().Refund(CreateStaff(StaffRole.BranchManager), basket, "Valid reason", []));

		Assert.Equal(TransactionPaymentStatus.Refunded, basket.PaymentStatus);
	}

	[Fact]
	public async Task ConcurrentVoidRequests_ProduceOneTransitionAndOneNotification()
	{
		var basket = CreateBasket();
		var rule = CreateRule();
		var attempts = Enumerable.Range(0, 12)
			.Select(index => Task.Run(() =>
			{
				try
				{
					rule.Void(
						CreateStaff(StaffRole.BranchManager),
						basket,
						$"Reason {index}", []);
					return true;
				}
				catch (InvalidOperationException)
				{
					return false;
				}
			}))
			.ToArray();

		var succeeded = await Task.WhenAll(attempts);

		Assert.Equal(1, succeeded.Count(result => result));
		Assert.Equal(TransactionPaymentStatus.Voided, basket.PaymentStatus);
		var notification = Assert.IsType<TransactionVoidedNotification>(
			Assert.Single(basket.Notifications));
		Assert.StartsWith("Reason ", notification.Reason);
	}

	[Fact]
	public async Task ConcurrentRefundRequests_ProduceOneTransitionAndOneNotification()
	{
		var basket = CreateBasket();
		basket.Pay();
		var rule = CreateRule();
		var attempts = Enumerable.Range(0, 12)
			.Select(index => Task.Run(() =>
			{
				try
				{
					rule.Refund(
						CreateStaff(StaffRole.BranchManager),
						basket,
						$"Reason {index}", []);
					return true;
				}
				catch (InvalidOperationException)
				{
					return false;
				}
			}))
			.ToArray();

		var succeeded = await Task.WhenAll(attempts);

		Assert.Equal(1, succeeded.Count(result => result));
		Assert.Equal(TransactionPaymentStatus.Refunded, basket.PaymentStatus);
		var notification = Assert.IsType<TransactionRefundedNotification>(
			Assert.Single(basket.Notifications));
		Assert.StartsWith("Reason ", notification.Reason);
	}

	[Fact]
	public void Void_WithNullStaff_ThrowsArgumentNullExceptionWithoutChangingStatus()
	{
		var basket = CreateBasket();

		Assert.Throws<ArgumentNullException>(() =>
			CreateRule().Void(null!, basket, "Valid reason", []));

		Assert.Equal(TransactionPaymentStatus.Open, basket.PaymentStatus);
	}

	[Fact]
	public void Refund_WithNullBasket_ThrowsArgumentNullException()
	{
		Assert.Throws<ArgumentNullException>(() =>
			CreateRule().Refund(CreateStaff(StaffRole.BranchManager), null!, "Valid reason", []));
	}

	private static TransactionVoidRefundRule CreateRule() => new();

	private static BranchStock CreateStock(string sku, int quantity) =>
		new(new BranchCode("DEL01"), new Sku(sku), quantity);

	private static TransactionLineItem CreateGoodsLine(string sku, int quantity) =>
		new(
			"Box",
			TransactionItemType.Goods,
			new Money(10m, "INR"),
			quantity,
			TaxCategory.Standard,
			new Sku(sku));

	private static TransactionBasket CreateSoldBasket(
		BranchStock stock,
		int quantity)
	{
		var basket = CreateEmptyBasket();
		basket.AddLineItem(CreateGoodsLine(stock.ProductSku.Value, quantity));
		new SaleProcessingService().ProcessSale(
			basket, stock.BranchCode, [stock]);
		return basket;
	}

	private static Staff CreateStaff(StaffRole role) =>
		new("STAFF01", "Staff Member", "staff@example.com", role, new BranchCode("DEL01"));

	private static TransactionBasket CreateEmptyBasket() =>
		new(
			"INR",
			new Dictionary<TaxCategory, decimal>
			{
				[TaxCategory.Standard] = 0.1m
			},
			new BranchCode("DEL01"));

	private static TransactionBasket CreateBasket()
	{
		var basket = CreateEmptyBasket();
		basket.AddLineItem(new TransactionLineItem(
			"Goods",
			TransactionItemType.Goods,
			new Money(10m, "INR"),
			1,
			TaxCategory.Standard));
		return basket;
	}
}
