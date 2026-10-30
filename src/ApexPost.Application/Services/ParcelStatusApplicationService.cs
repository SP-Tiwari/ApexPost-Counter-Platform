using ApexPost.Application.Courier;

namespace ApexPost.Application.Services;

// What a courier partner may see about one shipment: facts already stored by
// the counter, the existing transaction status, and the courier's own lookup.
// No prices, staff, sender, recipient, or identity data is included, and no
// new parcel-status vocabulary is introduced.
public sealed record ParcelStatus(
	string ShipmentId,
	ShippingItemType Type,
	ShippingServiceLevel ServiceLevel,
	string DestinationZone,
	DateTimeOffset? AcceptedAtUtc,
	TransactionPaymentStatus TransactionStatus,
	CourierTrackingResult? Courier);

public sealed class ParcelStatusApplicationService(
	IApexPostRepository repository,
	ICourierTrackingService? courier = null)
{
	private readonly IApexPostRepository _repository = repository
		?? throw new ArgumentNullException(nameof(repository));

	// Returns null when no transaction contains the shipment.
	public async Task<ParcelStatus?> GetStatusAsync(
		string shipmentId,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(shipmentId);

		var snapshot = await _repository.LoadAsync(cancellationToken)
			.ConfigureAwait(false);
		var match = snapshot.TransactionBaskets
			.SelectMany(basket => basket.LineItems
				.Where(lineItem => string.Equals(
					lineItem.ShippingItem?.Id, shipmentId, StringComparison.Ordinal))
				.Select(lineItem => (Basket: basket, Shipment: lineItem.ShippingItem!)))
			.Take(2)
			.ToArray();
		if (match.Length != 1)
			return null;

		var (basket, shipment) = match[0];
		var courierResult = courier is null
			? null
			: await courier.GetTrackingAsync(shipment.Id, cancellationToken)
				.ConfigureAwait(false);

		return new ParcelStatus(
			shipment.Id,
			shipment.Type,
			shipment.ServiceLevel,
			shipment.DestinationZone,
			basket.OccurredAtUtc,
			basket.PaymentStatus,
			courierResult);
	}
}
