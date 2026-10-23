
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Tests.ValueObjects;

public class WeightTests
{
	[Fact]
	public void Constructor_WithPositiveWeight_CreatesWeight()
	{
		var weight = new Weight(2.5m);

		Assert.Equal(2.5m, weight.Kilograms);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void Constructor_WithZeroOrNegativeWeight_ThrowsException(
		decimal kilograms)
	{
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new Weight(kilograms));
	}

	[Fact]
	public void Exceeds_WhenWeightIsGreaterThanMaximum_ReturnsTrue()
	{
		var parcelWeight = new Weight(10m);
		var maximumWeight = new Weight(5m);

		Assert.True(parcelWeight.Exceeds(maximumWeight));
	}

	[Fact]
	public void Exceeds_WhenWeightIsBelowMaximum_ReturnsFalse()
	{
		var parcelWeight = new Weight(3m);
		var maximumWeight = new Weight(5m);

		Assert.False(parcelWeight.Exceeds(maximumWeight));
	}

	[Fact]
	public void Exceeds_WhenWeightEqualsMaximum_ReturnsFalse()
	{
		var parcelWeight = new Weight(5m);
		var maximumWeight = new Weight(5m);

		Assert.False(parcelWeight.Exceeds(maximumWeight));
	}

	[Fact]
	public void Exceeds_WhenMaximumWeightIsNull_ThrowsException()
	{
		var parcelWeight = new Weight(3m);

		Assert.Throws<ArgumentNullException>(
			() => parcelWeight.Exceeds(null!));
	}
}
