using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Tests.ValueObjects;

public class ShippingRateTests
{
	[Fact]
	public void Constructor_WithValidConfiguration_CreatesRate()
	{
		var rate = new ShippingRate(
			"Zone A",
			ShippingServiceLevel.Standard,
			new Money(10m, "INR"),
			new Money(2m, "INR"));

		Assert.Equal("Zone A", rate.DestinationZone);
		Assert.Equal(ShippingServiceLevel.Standard, rate.ServiceLevel);
		Assert.Equal(new Money(10m, "INR"), rate.BasePrice);
		Assert.Equal(new Money(2m, "INR"), rate.PerKilogramCharge);
	}

	[Theory]
	[InlineData("")]
	[InlineData(" ")]
	public void Constructor_WithEmptyDestinationZone_ThrowsArgumentException(
		string destinationZone)
	{
		Assert.Throws<ArgumentException>(() =>
			new ShippingRate(
				destinationZone,
				ShippingServiceLevel.Standard,
				new Money(10m, "INR"),
				new Money(2m, "INR")));
	}

	[Fact]
	public void Constructor_WithInvalidServiceLevel_ThrowsArgumentOutOfRangeException()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() =>
			new ShippingRate(
				"Zone A",
				(ShippingServiceLevel)int.MaxValue,
				new Money(10m, "INR"),
				new Money(2m, "INR")));
	}

	[Fact]
	public void Constructor_WithCurrencyMismatch_ThrowsArgumentException()
	{
		Assert.Throws<ArgumentException>(() =>
			new ShippingRate(
				"Zone A",
				ShippingServiceLevel.Standard,
				new Money(10m, "INR"),
				new Money(2m, "USD")));
	}

	[Fact]
	public void Constructor_WithMissingPrice_ThrowsArgumentNullException()
	{
		Assert.Throws<ArgumentNullException>(() =>
			new ShippingRate(
				"Zone A",
				ShippingServiceLevel.Standard,
				null!,
				new Money(2m, "INR")));
	}
}
