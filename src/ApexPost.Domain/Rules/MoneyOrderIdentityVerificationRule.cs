
namespace ApexPost.Domain.Rules;

public sealed class MoneyOrderIdentityVerificationRule
{
	private readonly Money _highValueThreshold;

	public MoneyOrderIdentityVerificationRule(Money highValueThreshold)
	{
		ArgumentNullException.ThrowIfNull(highValueThreshold);

		_highValueThreshold = highValueThreshold;
	}

	public void EnsureVerification(
		MoneyOrder moneyOrder,
		IdentityVerification? verification)
	{
		ArgumentNullException.ThrowIfNull(moneyOrder);

		if (!string.Equals(
			moneyOrder.Amount.Currency,
			_highValueThreshold.Currency,
			StringComparison.Ordinal))
		{
			throw new InvalidOperationException(
				"Money order amount and high-value threshold must use the same currency.");
		}

		if (moneyOrder.Amount.Amount > _highValueThreshold.Amount &&
			verification?.IsSuccessful != true)
		{
			throw new InvalidOperationException(
				"Successful identity verification is required for this money order.");
		}
	}
}
