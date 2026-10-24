using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;

namespace ApexPost.Domain.Reporting;

public sealed record ShippingVolumeReportRow(
	string DestinationZone,
	ShippingServiceLevel ServiceLevel,
	int ShipmentCount);

public sealed class ShippingVolumeReportService
{
	public IReadOnlyList<ShippingVolumeReportRow> CreateReport(
		IEnumerable<ShippingItem> shippingItems)
	{
		ArgumentNullException.ThrowIfNull(shippingItems);

		var counts = new Dictionary<(string DestinationZone, ShippingServiceLevel ServiceLevel), int>();
		foreach (var shippingItem in shippingItems)
		{
			ArgumentNullException.ThrowIfNull(shippingItem);

			var key = (shippingItem.DestinationZone, shippingItem.ServiceLevel);
			counts[key] = checked(counts.GetValueOrDefault(key) + 1);
		}

		return counts
			.Select(pair => new ShippingVolumeReportRow(
				pair.Key.DestinationZone,
				pair.Key.ServiceLevel,
				pair.Value))
			.OrderBy(row => row.DestinationZone, StringComparer.Ordinal)
			.ThenBy(row => row.ServiceLevel)
			.ToArray();
	}
}
