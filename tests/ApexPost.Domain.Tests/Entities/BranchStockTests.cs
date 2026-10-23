
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Tests.Entities;

public class BranchStockTests
{
	[Fact]
	public void Constructor_WithNegativeQuantity_ThrowsArgumentOutOfRangeException()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() =>
			new BranchStock(
				new BranchCode("DEL01"),
				new Sku("BOX01"),
				-1));
	}

	[Fact]
	public void AddStock_IncreasesQuantity()
	{
		var stock = CreateStock(10);

		stock.AddStock(5);

		Assert.Equal(15, stock.Quantity);
	}

	[Fact]
	public void RemoveStock_DecreasesQuantity()
	{
		var stock = CreateStock(10);

		stock.RemoveStock(4);

		Assert.Equal(6, stock.Quantity);
	}

	[Fact]
	public void RemoveStock_WhenInsufficient_ThrowsInvalidOperationException()
	{
		var stock = CreateStock(3);

		Assert.Throws<InvalidOperationException>(() => stock.RemoveStock(4));
		Assert.Equal(3, stock.Quantity);
	}

	private static BranchStock CreateStock(int quantity) =>
		new(new BranchCode("DEL01"), new Sku("BOX01"), quantity);
}
