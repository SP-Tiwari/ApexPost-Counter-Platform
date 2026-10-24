using ApexPost.Domain.Entities;
using ApexPost.Domain.Notifications;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Rules;

public sealed class HighValueMoneyOrderNotificationRule
{
	private readonly Money _threshold;

	public HighValueMoneyOrderNotificationRule(Money threshold)
	{
		ArgumentNullException.ThrowIfNull(threshold);
		_threshold = threshold;
	}

	public HighValueMoneyOrderNotification? Evaluate(MoneyOrder moneyOrder)
	{
		ArgumentNullException.ThrowIfNull(moneyOrder);

		if (!string.Equals(
			moneyOrder.Amount.Currency,
			_threshold.Currency,
			StringComparison.Ordinal))
		{
			throw new InvalidOperationException(
				"Money order amount and notification threshold must use the same currency.");
		}

		return moneyOrder.Amount.Amount > _threshold.Amount
			? new HighValueMoneyOrderNotification(moneyOrder.Amount, _threshold)
			: null;
	}
}
