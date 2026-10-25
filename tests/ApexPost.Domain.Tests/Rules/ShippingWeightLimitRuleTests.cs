
namespace ApexPost.Domain.Tests.Rules;

public class ShippingWeightLimitRuleTests
{
	[Fact]
	public void EnsureWithinLimit_WhenParcelIsWithinLimit_DoesNotThrow()
	{
		var rule = CreateRule();

		rule.EnsureWithinLimit(CreateShippingItem(
			ShippingItemType.Parcel,
			4m,
			ShippingServiceLevel.Standard));
	}

	[Fact]
	public void EnsureWithinLimit_WhenParcelExceedsLimit_ThrowsInvalidOperationException()
	{
		var rule = CreateRule();

		var exception = Assert.Throws<InvalidOperationException>(() =>
			rule.EnsureWithinLimit(CreateShippingItem(
				ShippingItemType.Parcel,
				5.01m,
				ShippingServiceLevel.Standard)));

		Assert.Contains("exceeds the maximum of 5 kg", exception.Message);
	}

	[Fact]
	public void EnsureWithinLimit_WhenParcelEqualsLimit_DoesNotThrow()
	{
		var rule = CreateRule();

		rule.EnsureWithinLimit(CreateShippingItem(
			ShippingItemType.Parcel,
			5m,
			ShippingServiceLevel.Standard));
	}

	[Fact]
	public void EnsureWithinLimit_WhenLetterExceedsParcelLimit_DoesNotThrow()
	{
		var rule = CreateRule();

		rule.EnsureWithinLimit(CreateShippingItem(
			ShippingItemType.Letter,
			10m,
			ShippingServiceLevel.Standard));
	}

	[Fact]
	public void EnsureWithinLimit_WhenParcelServiceLevelIsNotConfigured_ThrowsInvalidOperationException()
	{
		var rule = new ShippingWeightLimitRule(
			new Dictionary<ShippingServiceLevel, Weight>());

		Assert.Throws<InvalidOperationException>(() =>
			rule.EnsureWithinLimit(CreateShippingItem(
				ShippingItemType.Parcel,
				1m,
				ShippingServiceLevel.Registered)));
	}

	[Fact]
	public void EnsureWithinLimit_WhenLetterServiceLevelIsNotConfigured_DoesNotThrow()
	{
		var rule = new ShippingWeightLimitRule(
			new Dictionary<ShippingServiceLevel, Weight>());

		rule.EnsureWithinLimit(CreateShippingItem(
			ShippingItemType.Letter,
			10m,
			ShippingServiceLevel.Registered));
	}

	[Fact]
	public void Constructor_WithNullConfiguration_ThrowsArgumentNullException()
	{
		Assert.Throws<ArgumentNullException>(() =>
			new ShippingWeightLimitRule(null!));
	}

	[Fact]
	public void EnsureWithinLimit_WithNullShippingItem_ThrowsArgumentNullException()
	{
		var rule = CreateRule();

		Assert.Throws<ArgumentNullException>(() =>
			rule.EnsureWithinLimit(null!));
	}

	private static ShippingWeightLimitRule CreateRule() =>
		new(new Dictionary<ShippingServiceLevel, Weight>
		{
			[ShippingServiceLevel.Standard] = new Weight(5m),
			[ShippingServiceLevel.Express] = new Weight(3m)
		});

	private static ShippingItem CreateShippingItem(
		ShippingItemType type,
		decimal kilograms,
		ShippingServiceLevel serviceLevel) =>
		new(
			"shipping-item-1",
			type,
			new Weight(kilograms),
			new Dimensions(10m, 10m, 10m),
			"Zone A",
			serviceLevel);
}
