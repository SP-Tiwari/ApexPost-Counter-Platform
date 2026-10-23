namespace ApexPost.Domain.ValueObjects;

public sealed record Money
{
	public decimal Amount { get; }
	public string Currency { get; }

	public Money(decimal amount, string currency)
	{
		if (amount < 0)
		{
			throw new ArgumentOutOfRangeException(
				nameof(amount), "Money amount cannot be negative.");
		}

		if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
		{
			throw new ArgumentException(
				"Currency must be a three-letter code, such as INR.",
				nameof(currency));
		}

		Amount = amount;
		Currency = currency.ToUpperInvariant();
	}
}
