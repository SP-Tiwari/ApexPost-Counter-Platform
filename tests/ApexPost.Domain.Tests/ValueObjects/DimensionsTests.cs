
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Tests.ValueObjects;

public class DimensionsTests
{
	[Fact]
	public void Constructor_WithPositiveDimensions_CreatesDimensions()
	{
		var dimensions = new Dimensions(10, 20, 30);

		Assert.Equal(10, dimensions.LengthCm);
		Assert.Equal(20, dimensions.WidthCm);
		Assert.Equal(30, dimensions.HeightCm);
	}

	[Fact]
	public void Constructor_WithZeroLength_ThrowsArgumentOutOfRangeException()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() =>
			new Dimensions(0, 20, 30));
	}
}
