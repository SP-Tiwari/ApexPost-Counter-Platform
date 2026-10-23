
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Tests.ValueObjects;

public class SkuTests
{
	[Fact]
	public void Constructor_WithValidSku_CreatesSku()
	{
		var sku = new Sku("BOX-SMALL-001");

		Assert.Equal("BOX-SMALL-001", sku.Value);
	}

	[Fact]
	public void Constructor_WithLowercaseSku_ConvertsToUppercase()
	{
		var sku = new Sku("box-small-001");

		Assert.Equal("BOX-SMALL-001", sku.Value);
	}

	[Fact]
	public void Constructor_WithWhitespace_TrimsSku()
	{
		var sku = new Sku(" box-small-001 ");

		Assert.Equal("BOX-SMALL-001", sku.Value);
	}

	[Theory]
	[InlineData("")]
	[InlineData(" ")]
	[InlineData(null)]
	public void Constructor_WithEmptySku_ThrowsException(string? value)
	{
		Assert.Throws<ArgumentException>(
			() => new Sku(value!));
	}

	[Fact]
	public void Constructor_WithSkuLongerThan30Characters_ThrowsException()
	{
		var longSku = new string('A', 31);

		Assert.Throws<ArgumentException>(
			() => new Sku(longSku));
	}

	[Fact]
	public void ToString_ReturnsSkuValue()
	{
		var sku = new Sku("BOX-SMALL-001");

		Assert.Equal("BOX-SMALL-001", sku.ToString());
	}
}