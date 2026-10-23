
using ApexPost.Domain.Enums;

namespace ApexPost.Domain.ValueObjects;

public sealed record ShippingRate
{
	public string DestinationZone { get; }
	public ShippingServiceLevel ServiceLevel { get; }
	public Weight WeightBandMaximum { get; }
	public Money Price { get; }

	public ShippingRate(
		string destinationZone,
		ShippingServiceLevel serviceLevel,
		Weight weightBandMaximum,
		Money price)
	{
		if (string.IsNullOrWhiteSpace(destinationZone))
			throw new ArgumentException(
				"Destination zone is required.", nameof(destinationZone));

		if (!Enum.IsDefined(serviceLevel))
			throw new ArgumentOutOfRangeException(nameof(serviceLevel));

		ArgumentNullException.ThrowIfNull(weightBandMaximum);
		ArgumentNullException.ThrowIfNull(price);

		DestinationZone = destinationZone.Trim();
		ServiceLevel = serviceLevel;
		WeightBandMaximum = weightBandMaximum;
		Price = price;
	}
}
