using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Entities;

public sealed class MoneyOrder
{
	public Money Amount { get; }
	public string Sender { get; }
	public string Recipient { get; }
	public Money Fee { get; }

	public MoneyOrder(
		Money amount,
		string sender,
		string recipient,
		Money fee)
	{
		ArgumentNullException.ThrowIfNull(amount);
		ArgumentNullException.ThrowIfNull(fee);

		if (amount.Amount <= 0)
			throw new ArgumentOutOfRangeException(
				nameof(amount), "Money order amount must be greater than zero.");

		if (string.IsNullOrWhiteSpace(sender))
			throw new ArgumentException(
				"Money order sender is required.", nameof(sender));

		if (string.IsNullOrWhiteSpace(recipient))
			throw new ArgumentException(
				"Money order recipient is required.", nameof(recipient));

		if (!string.Equals(amount.Currency, fee.Currency, StringComparison.Ordinal))
			throw new ArgumentException(
				"Money order amount and fee must use the same currency.",
				nameof(fee));

		Amount = amount;
		Sender = sender.Trim();
		Recipient = recipient.Trim();
		Fee = fee;
	}
}
