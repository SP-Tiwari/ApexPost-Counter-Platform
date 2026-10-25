using ApexPost.Domain.Reporting;

namespace ApexPost.Domain.Tests.Reporting;

public class DomainReportServiceTests
{
	[Fact]
	public void DailySalesReport_GroupsByBranchAndDateAndSeparatesGoodsAndServices()
	{
		var report = new DailySalesReportService().CreateReport(
		[
			CreateSalesEntry("DEL01", new DateOnly(2026, 10, 24), 10m, 20m),
			CreateSalesEntry("DEL01", new DateOnly(2026, 10, 24), 5m, 0m),
			CreateSalesEntry("MUM01", new DateOnly(2026, 10, 24), 7m, 3m),
			CreateSalesEntry("DEL01", new DateOnly(2026, 10, 25), 2m, 4m)
		]);

		Assert.Equal(3, report.Count);

		var delhiDayOne = Assert.Single(report, row =>
			row.BranchCode.Value == "DEL01" &&
			row.BusinessDate == new DateOnly(2026, 10, 24));
		Assert.Equal(new Money(16.5m, "INR"), delhiDayOne.RetailSales);
		Assert.Equal(new Money(22m, "INR"), delhiDayOne.ServiceSales);
		Assert.Equal(new Money(38.5m, "INR"), delhiDayOne.TotalSales);

		var mumbai = Assert.Single(report, row => row.BranchCode.Value == "MUM01");
		Assert.Equal(new Money(7.7m, "INR"), mumbai.RetailSales);
		Assert.Equal(new Money(3.3m, "INR"), mumbai.ServiceSales);
	}

	[Fact]
	public void DailySalesReportEntry_RejectsUnpaidBasket()
	{
		Assert.Throws<InvalidOperationException>(() =>
			new DailySalesReportEntry(
				new BranchCode("DEL01"),
				new DateOnly(2026, 10, 24),
				CreateBasket(10m, 0m, paid: false)));
	}

	[Fact]
	public void DailySalesReport_ExcludesBasketRefundedAfterEntryCreation()
	{
		var basket = CreateBasket(10m, 0m, paid: true);
		var entry = new DailySalesReportEntry(
			new BranchCode("DEL01"),
			new DateOnly(2026, 10, 24),
			basket);
		new TransactionVoidRefundRule().Refund(
			new Staff(
				"MANAGER01",
				"Branch Manager",
				"manager@example.com",
				StaffRole.BranchManager,
				new BranchCode("DEL01")),
			basket,
			"Customer refund");

		var report = new DailySalesReportService().CreateReport([entry]);

		Assert.Empty(report);
	}

	[Fact]
	public void DailySalesReport_RejectsCurrencyMismatchWithinBranchAndDate()
	{
		var entries = new[]
		{
			CreateSalesEntry("DEL01", new DateOnly(2026, 10, 24), 10m, 0m, "INR"),
			CreateSalesEntry("DEL01", new DateOnly(2026, 10, 24), 10m, 0m, "USD")
		};

		Assert.Throws<InvalidOperationException>(() =>
			new DailySalesReportService().CreateReport(entries));
	}

	[Fact]
	public void LowStockReport_UsesCallerThresholdAndKeepsBranchesSeparate()
	{
		var report = new LowStockReportService().CreateReport(
		[
			CreateStock("DEL01", "BOX01", 2),
			CreateStock("MUM01", "BOX01", 3),
			CreateStock("DEL01", "PEN01", 4)
		],
		3);

		Assert.Equal(2, report.Count);
		Assert.Contains(report, row =>
			row.BranchCode.Value == "DEL01" &&
			row.ProductSku.Value == "BOX01" &&
			row.Quantity == 2 &&
			row.Threshold == 3);
		Assert.Contains(report, row =>
			row.BranchCode.Value == "MUM01" &&
			row.Quantity == 3);
	}

	[Fact]
	public void LowStockReport_WithNegativeThreshold_ThrowsArgumentOutOfRangeException()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() =>
			new LowStockReportService().CreateReport([], -1));
	}

	[Fact]
	public void ShippingVolumeReport_GroupsByDestinationZoneAndServiceLevel()
	{
		var report = new ShippingVolumeReportService().CreateReport(
		[
			CreateShippingItem("ZONE-A", ShippingServiceLevel.Standard),
			CreateShippingItem("ZONE-A", ShippingServiceLevel.Standard),
			CreateShippingItem("ZONE-A", ShippingServiceLevel.Express),
			CreateShippingItem("ZONE-B", ShippingServiceLevel.Standard)
		]);

		Assert.Equal(3, report.Count);
		Assert.Contains(report, row =>
			row.DestinationZone == "ZONE-A" &&
			row.ServiceLevel == ShippingServiceLevel.Standard &&
			row.ShipmentCount == 2);
		Assert.Contains(report, row =>
			row.DestinationZone == "ZONE-A" &&
			row.ServiceLevel == ShippingServiceLevel.Express &&
			row.ShipmentCount == 1);
		Assert.Contains(report, row =>
			row.DestinationZone == "ZONE-B" &&
			row.ServiceLevel == ShippingServiceLevel.Standard &&
			row.ShipmentCount == 1);
	}

	[Fact]
	public void BranchAuditorReport_CountsMoneyOrdersAndVerificationOutcomesPerBranch()
	{
		var report = new BranchAuditorReportService().CreateReport(
		[
			new BranchAuditorReportEntry(
				new BranchCode("DEL01"),
				CreateMoneyOrder(),
				new IdentityVerification("Passport", "ABCD1234", true)),
			new BranchAuditorReportEntry(
				new BranchCode("DEL01"),
				CreateMoneyOrder(),
				new IdentityVerification("Passport", "EFGH5678", false)),
			new BranchAuditorReportEntry(
				new BranchCode("MUM01"),
				CreateMoneyOrder(),
				null),
			new BranchAuditorReportEntry(
				new BranchCode("MUM01"),
				null,
				new IdentityVerification("Passport", "IJKL9012", true))
		]);

		Assert.Equal(2, report.Count);
		var delhi = Assert.Single(report, row => row.BranchCode.Value == "DEL01");
		Assert.Equal(2, delhi.MoneyOrderCount);
		Assert.Equal(2, delhi.IdentityVerificationCount);
		Assert.Equal(1, delhi.SuccessfulIdentityVerificationCount);
		Assert.Equal(1, delhi.FailedIdentityVerificationCount);

		var mumbai = Assert.Single(report, row => row.BranchCode.Value == "MUM01");
		Assert.Equal(1, mumbai.MoneyOrderCount);
		Assert.Equal(1, mumbai.IdentityVerificationCount);
		Assert.Equal(1, mumbai.SuccessfulIdentityVerificationCount);
		Assert.Equal(0, mumbai.FailedIdentityVerificationCount);
	}

	[Fact]
	public void BranchAuditorReportEntry_RejectsEntryWithoutReportableActivity()
	{
		Assert.Throws<ArgumentException>(() =>
			new BranchAuditorReportEntry(new BranchCode("DEL01"), null, null));
	}

	private static DailySalesReportEntry CreateSalesEntry(
		string branch,
		DateOnly date,
		decimal goods,
		decimal services,
		string currency = "INR") =>
		new(
			new BranchCode(branch),
			date,
			CreateBasket(goods, services, paid: true, currency));

	private static TransactionBasket CreateBasket(
		decimal goods,
		decimal services,
		bool paid,
		string currency = "INR")
	{
		var basket = new TransactionBasket(
			currency,
			new Dictionary<TaxCategory, decimal>
			{
				[TaxCategory.Standard] = 0.1m
			},
			new BranchCode("DEL01"));

		if (goods > 0m)
		{
			basket.AddLineItem(new TransactionLineItem(
				"Goods",
				TransactionItemType.Goods,
				new Money(goods, currency),
				1,
				TaxCategory.Standard));
		}

		if (services > 0m)
		{
			basket.AddLineItem(new TransactionLineItem(
				"Service",
				TransactionItemType.Service,
				new Money(services, currency),
				1,
				TaxCategory.Standard));
		}

		if (paid)
			basket.Pay();

		return basket;
	}

	private static BranchStock CreateStock(string branch, string sku, int quantity) =>
		new(new BranchCode(branch), new Sku(sku), quantity);

	private static ShippingItem CreateShippingItem(
		string zone,
		ShippingServiceLevel serviceLevel) =>
		new(
			Guid.NewGuid().ToString(),
			ShippingItemType.Parcel,
			new Weight(1m),
			new Dimensions(1m, 1m, 1m),
			zone,
			serviceLevel);

	private static MoneyOrder CreateMoneyOrder() =>
		new(
			new Money(100m, "INR"),
			"Sender",
			"Recipient",
			new Money(5m, "INR"));
}
