
namespace ApexPost.Domain.ValueObjects;

public sealed record Dimensions
{
	public decimal LengthCm { get; }
	public decimal WidthCm { get; }
	public decimal HeightCm { get; }

	public Dimensions(decimal lengthCm, decimal widthCm, decimal heightCm)
	{
		if (lengthCm <= 0)
			throw new ArgumentOutOfRangeException(nameof(lengthCm));

		if (widthCm <= 0)
			throw new ArgumentOutOfRangeException(nameof(widthCm));

		if (heightCm <= 0)
			throw new ArgumentOutOfRangeException(nameof(heightCm));

		LengthCm = lengthCm;
		WidthCm = widthCm;
		HeightCm = heightCm;
	}
}
