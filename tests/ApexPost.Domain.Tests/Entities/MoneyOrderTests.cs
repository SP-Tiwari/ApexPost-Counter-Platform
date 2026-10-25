
namespace ApexPost.Domain.Tests.Entities;

public class MoneyOrderTests
{
	[Fact]
	public void Constructor_WithValidDetails_CreatesMoneyOrder()
	{
		var moneyOrder = new MoneyOrder(
			new Money(100m, "INR"),
			" Sender Name ",
			" Recipient Name ",
			new Money(5m, "INR"));

		Assert.Equal(new Money(100m, "INR"), moneyOrder.Amount);
		Assert.Equal("Sender Name", moneyOrder.Sender);
		Assert.Equal("Recipient Name", moneyOrder.Recipient);
		Assert.Equal(new Money(5m, "INR"), moneyOrder.Fee);
	}

	[Fact]
	public void Constructor_WithZeroAmount_ThrowsArgumentOutOfRangeException()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() =>
			new MoneyOrder(
				new Money(0m, "INR"),
				"Sender",
				"Recipient",
				new Money(5m, "INR")));
	}

	[Fact]
	public void Constructor_WithNegativeAmount_ThrowsArgumentOutOfRangeException()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() =>
			new Money(-1m, "INR"));
	}

	[Fact]
	public void Constructor_WithNegativeFee_ThrowsArgumentOutOfRangeException()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() =>
			new Money(-1m, "INR"));
	}

	[Fact]
	public void Constructor_WithCurrencyMismatch_ThrowsArgumentException()
	{
		Assert.Throws<ArgumentException>(() =>
			new MoneyOrder(
				new Money(100m, "INR"),
				"Sender",
				"Recipient",
				new Money(5m, "USD")));
	}

	[Theory]
	[InlineData("")]
	[InlineData(" ")]
	public void Constructor_WithMissingSender_ThrowsArgumentException(string sender)
	{
		Assert.Throws<ArgumentException>(() =>
			new MoneyOrder(
				new Money(100m, "INR"),
				sender,
				"Recipient",
				new Money(5m, "INR")));
	}

	[Theory]
	[InlineData("")]
	[InlineData(" ")]
	public void Constructor_WithMissingRecipient_ThrowsArgumentException(string recipient)
	{
		Assert.Throws<ArgumentException>(() =>
			new MoneyOrder(
				new Money(100m, "INR"),
				"Sender",
				recipient,
				new Money(5m, "INR")));
	}

	[Fact]
	public void Constructor_WithMissingAmountOrFee_ThrowsArgumentNullException()
	{
		Assert.Throws<ArgumentNullException>(() =>
			new MoneyOrder(
				null!,
				"Sender",
				"Recipient",
				new Money(5m, "INR")));

		Assert.Throws<ArgumentNullException>(() =>
			new MoneyOrder(
				new Money(100m, "INR"),
				"Sender",
				"Recipient",
				null!));
	}
}
