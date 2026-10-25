
namespace ApexPost.Domain.Tests.Entities;

public class TransactionLineItemTests
{
	[Fact]
	public void Constructor_WithValidDetails_CreatesLineAndCalculatesSubtotal()
	{
		var lineItem = CreateLineItem(12m, 3);

		Assert.Equal("Sample item", lineItem.Description);
		Assert.Equal(TransactionItemType.Goods, lineItem.Type);
		Assert.Equal(3, lineItem.Quantity);
		Assert.Equal(new Money(36m, "INR"), lineItem.Subtotal);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void Constructor_WithNonPositiveQuantity_ThrowsArgumentOutOfRangeException(
		int quantity)
	{
		Assert.Throws<ArgumentOutOfRangeException>(() =>
			CreateLineItem(12m, quantity));
	}

	[Fact]
	public void Constructor_WithNegativeUnitPrice_ThrowsArgumentOutOfRangeException()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() =>
			new TransactionLineItem(
				"Sample item",
				TransactionItemType.Goods,
				new Money(-1m, "INR"),
				1,
				TaxCategory.Standard));
	}

	[Fact]
	public void Constructor_WithInvalidItemType_ThrowsArgumentOutOfRangeException()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() =>
			new TransactionLineItem(
				"Sample item",
				(TransactionItemType)int.MaxValue,
				new Money(12m, "INR"),
				1,
				TaxCategory.Standard));
	}

	[Fact]
	public void Constructor_WithInvalidTaxCategory_ThrowsArgumentOutOfRangeException()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() =>
			new TransactionLineItem(
				"Sample item",
				TransactionItemType.Goods,
				new Money(12m, "INR"),
				1,
				(TaxCategory)int.MaxValue));
	}

	private static TransactionLineItem CreateLineItem(
		decimal unitPrice,
		int quantity) =>
		new(
			" Sample item ",
			TransactionItemType.Goods,
			new Money(unitPrice, "INR"),
			quantity,
			TaxCategory.Standard);
}
