namespace ApexPost.Domain.ValueObjects;

public sealed record Sku
{
	public string Value { get; }

	public Sku(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			throw new ArgumentException(
				"SKU cannot be empty.",
				nameof(value));
		}

		value = value.Trim().ToUpperInvariant();

		if (value.Length > 30)
		{
			throw new ArgumentException(
				"SKU cannot exceed 30 characters.",
				nameof(value));
		}

		Value = value;
	}

	public override string ToString() => Value;
}
