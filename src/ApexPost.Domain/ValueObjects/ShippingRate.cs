
using ApexPost.Domain.Enums;

namespace ApexPost.Domain.ValueObjects;

public sealed record ShippingRate
{
	public string DestinationZone { get; }
	public ShippingServiceLevel ServiceLevel { get; }
	public Money BasePrice { get; }
	public Money PerKilogramCharge { get; }

	public ShippingRate(
		string destinationZone,
		ShippingServiceLevel serviceLevel,
		Money basePrice,
		Money perKilogramCharge)
	{
		if (string.IsNullOrWhiteSpace(destinationZone))
			throw new ArgumentException(
				"Destination zone is required.", nameof(destinationZone));

		if (!Enum.IsDefined(serviceLevel))
			throw new ArgumentOutOfRangeException(nameof(serviceLevel));

		ArgumentNullException.ThrowIfNull(basePrice);
		ArgumentNullException.ThrowIfNull(perKilogramCharge);

		DestinationZone = destinationZone.Trim();
		ServiceLevel = serviceLevel;

		if (!string.Equals(
			basePrice.Currency,
			perKilogramCharge.Currency,
			StringComparison.Ordinal))
		{
			throw new ArgumentException(
				"Base price and per-kilogram charge must use the same currency.",
				nameof(perKilogramCharge));
		}

		BasePrice = basePrice;
		PerKilogramCharge = perKilogramCharge;
	}
}
