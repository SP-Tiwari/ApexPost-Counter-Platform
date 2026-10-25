
namespace ApexPost.Domain.Rules;

public sealed class ShippingWeightLimitRule
{
	private readonly IReadOnlyDictionary<ShippingServiceLevel, Weight> _maximumWeights;

	public ShippingWeightLimitRule(
		IReadOnlyDictionary<ShippingServiceLevel, Weight> maximumWeights)
	{
		ArgumentNullException.ThrowIfNull(maximumWeights);

		var configuredWeights = new Dictionary<ShippingServiceLevel, Weight>();
		foreach (var (serviceLevel, maximumWeight) in maximumWeights)
		{
			if (!Enum.IsDefined(serviceLevel))
				throw new ArgumentOutOfRangeException(nameof(maximumWeights));

			ArgumentNullException.ThrowIfNull(maximumWeight);
			configuredWeights.Add(serviceLevel, maximumWeight);
		}

		_maximumWeights = configuredWeights;
	}

	public void EnsureWithinLimit(ShippingItem shippingItem)
	{
		ArgumentNullException.ThrowIfNull(shippingItem);

		if (shippingItem.Type == ShippingItemType.Letter)
			return;

		if (!_maximumWeights.TryGetValue(shippingItem.ServiceLevel, out var maximumWeight))
		{
			throw new InvalidOperationException(
				$"No maximum parcel weight is configured for service level '{shippingItem.ServiceLevel}'.");
		}

		if (shippingItem.Weight.Exceeds(maximumWeight))
		{
			throw new InvalidOperationException(
				$"Parcel weight of {shippingItem.Weight.Kilograms} kg exceeds the maximum of {maximumWeight.Kilograms} kg for service level '{shippingItem.ServiceLevel}'.");
		}
	}
}
