
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Tests.ValueObjects;

public class MoneyTests
{
	[Fact]
	public void Constructor_WithValidAmountAndCurrency_CreatesMoney()
	{
		var money = new Money(100m, "INR");

		Assert.Equal(100m, money.Amount);
		Assert.Equal("INR", money.Currency);
	}

	[Fact]
	public void Constructor_WithNegativeAmount_ThrowsException()
	{
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new Money(-10m, "INR"));
	}

	[Theory]
	[InlineData("")]
	[InlineData("IN")]
	[InlineData("INRR")]
	public void Constructor_WithInvalidCurrency_ThrowsException(
		string currency)
	{
		Assert.Throws<ArgumentException>(
			() => new Money(100m, currency));
	}

	[Fact]
	public void Constructor_WithLowercaseCurrency_ConvertsToUppercase()
	{
		var money = new Money(50m, "inr");

		Assert.Equal("INR", money.Currency);
	}

	[Fact]
	public void TwoMoneyRecords_WithSameValues_AreEqual()
	{
		var first = new Money(100m, "INR");
		var second = new Money(100m, "INR");

		Assert.Equal(first, second);
	}
}
