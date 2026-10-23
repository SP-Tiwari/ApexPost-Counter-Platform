namespace ApexPost.Domain.ValueObjects;

public sealed record Weight
{
	public decimal Kilograms { get; }

	public Weight(decimal kilograms)
	{
		if (kilograms <= 0)
		{
			throw new ArgumentOutOfRangeException(
				nameof(kilograms),
				"Weight must be greater than zero.");
		}

		Kilograms = kilograms;
	}

	public bool Exceeds(Weight maximumWeight)
	{
		ArgumentNullException.ThrowIfNull(maximumWeight);

		return Kilograms > maximumWeight.Kilograms;
	}
}
