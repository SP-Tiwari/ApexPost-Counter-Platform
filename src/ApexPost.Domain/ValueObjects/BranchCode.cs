namespace ApexPost.Domain.ValueObjects;

public sealed record BranchCode
{
	public string Value { get; }

	public BranchCode(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			throw new ArgumentException(
				"Branch code cannot be empty.",
				nameof(value));
		}

		value = value.Trim().ToUpperInvariant();

		if (value.Length > 10 || value.Length < 2)
		{
			throw new ArgumentException(
				"Branch code must contain between 2 and 10 characters.",
				nameof(value));
		}

		Value = value;
	}

	public override string ToString() => Value;
}
