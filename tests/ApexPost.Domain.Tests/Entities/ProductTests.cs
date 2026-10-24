
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Tests.Entities;

public class ProductTests
{
	[Fact]
	public void Constructor_WithValidDetails_CreatesProduct()
	{
		var product = new Product(
			new Sku("BOX01"),
			"Small Box",
			ProductCategory.Packaging,
			new Money(10m, "INR"),
			TaxCategory.Standard);

		Assert.Equal("BOX01", product.Sku.Value);
		Assert.Equal("Small Box", product.Name);
		Assert.Equal(ProductCategory.Packaging, product.Category);
		Assert.Equal(10m, product.UnitPrice.Amount);
		Assert.Equal(TaxCategory.Standard, product.TaxCategory);
	}

	[Fact]
	public void Constructor_WithEmptyName_ThrowsArgumentException()
	{
		Assert.Throws<ArgumentException>(() =>
			new Product(
				new Sku("BOX01"),
				" ",
				ProductCategory.Packaging,
				new Money(10m, "INR"),
				TaxCategory.Standard));
	}
}
