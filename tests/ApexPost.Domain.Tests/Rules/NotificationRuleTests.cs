
namespace ApexPost.Domain.Tests.Rules;

public class NotificationRuleTests
{
	[Fact]
	public void LowStockNotificationRule_WhenQuantityIsAtThreshold_ReturnsNotification()
	{
		var notification = new LowStockNotificationRule().Evaluate(
			CreateStock(3),
			3);

		Assert.Equal(
			new LowStockNotification(
				new BranchCode("DEL01"),
				new Sku("BOX01"),
				3,
				3),
			notification);
	}

	[Fact]
	public void LowStockNotificationRule_WhenStockExceedsThreshold_ReturnsNull()
	{
		Assert.Null(new LowStockNotificationRule().Evaluate(CreateStock(4), 3));
	}

	[Fact]
	public void LowStockNotificationRule_WithNegativeThreshold_ThrowsArgumentOutOfRangeException()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() =>
			new LowStockNotificationRule().Evaluate(CreateStock(1), -1));
	}

	[Fact]
	public void HighValueMoneyOrderNotificationRule_WhenAmountExceedsThreshold_ReturnsNotification()
	{
		var threshold = new Money(100m, "INR");
		var notification = new HighValueMoneyOrderNotificationRule(threshold)
			.Evaluate(CreateMoneyOrder(100.01m));

		Assert.Equal(
			new HighValueMoneyOrderNotification(
				new Money(100.01m, "INR"),
				threshold),
			notification);
	}

	[Fact]
	public void HighValueMoneyOrderNotificationRule_WhenAmountEqualsThreshold_ReturnsNull()
	{
		Assert.Null(new HighValueMoneyOrderNotificationRule(
				new Money(100m, "INR"))
			.Evaluate(CreateMoneyOrder(100m)));
	}

	[Fact]
	public void HighValueMoneyOrderNotificationRule_WhenAmountIsBelowThreshold_ReturnsNull()
	{
		Assert.Null(new HighValueMoneyOrderNotificationRule(
				new Money(100m, "INR"))
			.Evaluate(CreateMoneyOrder(99.99m)));
	}

	[Fact]
	public void HighValueMoneyOrderNotificationRule_WhenCurrenciesMismatch_Throws()
	{
		var rule = new HighValueMoneyOrderNotificationRule(new Money(100m, "USD"));

		Assert.Throws<InvalidOperationException>(() =>
			rule.Evaluate(CreateMoneyOrder(50m)));
	}

	private static BranchStock CreateStock(int quantity) =>
		new(new BranchCode("DEL01"), new Sku("BOX01"), quantity);

	private static MoneyOrder CreateMoneyOrder(decimal amount) =>
		new(
			new Money(amount, "INR"),
			"Sender",
			"Recipient",
			new Money(5m, "INR"));
}
