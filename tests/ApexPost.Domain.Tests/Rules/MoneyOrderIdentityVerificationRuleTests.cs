using ApexPost.Domain.Entities;
using ApexPost.Domain.Rules;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Tests.Rules;

public class MoneyOrderIdentityVerificationRuleTests
{
	[Fact]
	public void EnsureVerification_WhenOrderIsBelowThreshold_AllowsMissingVerification()
	{
		var rule = CreateRule();

		rule.EnsureVerification(CreateMoneyOrder(99m), null);
	}

	[Fact]
	public void EnsureVerification_WhenOrderEqualsThreshold_AllowsMissingVerification()
	{
		var rule = CreateRule();

		rule.EnsureVerification(CreateMoneyOrder(100m), null);
	}

	[Fact]
	public void EnsureVerification_WhenOrderExceedsThresholdAndVerificationIsMissing_Throws()
	{
		var rule = CreateRule();

		Assert.Throws<InvalidOperationException>(() =>
			rule.EnsureVerification(CreateMoneyOrder(100.01m), null));
	}

	[Fact]
	public void EnsureVerification_WhenOrderExceedsThresholdAndVerificationFailed_Throws()
	{
		var rule = CreateRule();
		var verification = new IdentityVerification(
			"Passport",
			"ABCD1234",
			false);

		Assert.Throws<InvalidOperationException>(() =>
			rule.EnsureVerification(CreateMoneyOrder(101m), verification));
	}

	[Fact]
	public void EnsureVerification_WhenOrderExceedsThresholdAndVerificationSucceeded_AllowsOrder()
	{
		var rule = CreateRule();
		var verification = new IdentityVerification(
			"Passport",
			"ABCD1234",
			true);

		rule.EnsureVerification(CreateMoneyOrder(101m), verification);
	}

	[Fact]
	public void EnsureVerification_WhenCurrenciesDoNotMatch_ThrowsInvalidOperationException()
	{
		var rule = new MoneyOrderIdentityVerificationRule(new Money(100m, "USD"));

		Assert.Throws<InvalidOperationException>(() =>
			rule.EnsureVerification(CreateMoneyOrder(50m), null));
	}

	[Fact]
	public void Constructor_WithMissingThreshold_ThrowsArgumentNullException()
	{
		Assert.Throws<ArgumentNullException>(() =>
			new MoneyOrderIdentityVerificationRule(null!));
	}

	[Fact]
	public void EnsureVerification_WithMissingMoneyOrder_ThrowsArgumentNullException()
	{
		var rule = CreateRule();

		Assert.Throws<ArgumentNullException>(() =>
			rule.EnsureVerification(null!, null));
	}

	private static MoneyOrderIdentityVerificationRule CreateRule() =>
		new(new Money(100m, "INR"));

	private static MoneyOrder CreateMoneyOrder(decimal amount) =>
		new(
			new Money(amount, "INR"),
			"Sender",
			"Recipient",
			new Money(5m, "INR"));
}
