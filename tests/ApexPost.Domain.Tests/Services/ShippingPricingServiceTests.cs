using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.Services;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Tests.Services;

public class ShippingPricingServiceTests
{
	[Fact]
	public void CalculatePrice_WithConfiguredRate_CalculatesBaseAndWeightCharges()
	{
		var service = new ShippingPricingService(
		[
			new ShippingRate(
				"Zone A",
				ShippingServiceLevel.Standard,
				new Money(10m, "INR"),
				new Money(2m, "INR"))
		]);

		var price = service.CalculatePrice(CreateShippingItem(
			ShippingItemType.Parcel,
			2.5m,
			"Zone A",
			ShippingServiceLevel.Standard));

		Assert.Equal(new Money(15m, "INR"), price);
	}

	[Fact]
	public void CalculatePrice_WithDestinationZoneMismatch_ThrowsInvalidOperationException()
	{
		var service = new ShippingPricingService(
		[
			CreateRate("Zone A", ShippingServiceLevel.Standard)
		]);

		var exception = Assert.Throws<InvalidOperationException>(() =>
			service.CalculatePrice(CreateShippingItem(
				ShippingItemType.Parcel,
				1m,
				"Zone B",
				ShippingServiceLevel.Standard)));

		Assert.Contains("destination zone 'Zone B'", exception.Message);
	}

	[Fact]
	public void CalculatePrice_WithServiceLevelMismatch_ThrowsInvalidOperationException()
	{
		var service = new ShippingPricingService(
		[
			CreateRate("Zone A", ShippingServiceLevel.Standard)
		]);

		var exception = Assert.Throws<InvalidOperationException>(() =>
			service.CalculatePrice(CreateShippingItem(
				ShippingItemType.Parcel,
				1m,
				"Zone A",
				ShippingServiceLevel.Express)));

		Assert.Contains("service level 'Express'", exception.Message);
	}

	[Fact]
	public void CalculatePrice_WithNoRates_ThrowsInvalidOperationException()
	{
		var service = new ShippingPricingService([]);

		Assert.Throws<InvalidOperationException>(() =>
			service.CalculatePrice(CreateShippingItem(
				ShippingItemType.Parcel,
				1m,
				"Zone A",
				ShippingServiceLevel.Standard)));
	}

	[Fact]
	public void Constructor_WithDuplicateRateConfiguration_ThrowsArgumentException()
	{
		var rate = CreateRate("Zone A", ShippingServiceLevel.Standard);

		Assert.Throws<ArgumentException>(() =>
			new ShippingPricingService([rate, rate]));
	}

	[Fact]
	public void Constructor_WithNullRates_ThrowsArgumentNullException()
	{
		Assert.Throws<ArgumentNullException>(() =>
			new ShippingPricingService(null!));
	}

	[Fact]
	public void CalculatePrice_WithNullShippingItem_ThrowsArgumentNullException()
	{
		var service = new ShippingPricingService([]);

		Assert.Throws<ArgumentNullException>(() =>
			service.CalculatePrice(null!));
	}

	private static ShippingRate CreateRate(
		string destinationZone,
		ShippingServiceLevel serviceLevel) =>
		new(
			destinationZone,
			serviceLevel,
			new Money(10m, "INR"),
			new Money(2m, "INR"));

	private static ShippingItem CreateShippingItem(
		ShippingItemType type,
		decimal kilograms,
		string destinationZone,
		ShippingServiceLevel serviceLevel) =>
		new(
			"shipping-item-1",
			type,
			new Weight(kilograms),
			new Dimensions(10m, 10m, 10m),
			destinationZone,
			serviceLevel);
}
