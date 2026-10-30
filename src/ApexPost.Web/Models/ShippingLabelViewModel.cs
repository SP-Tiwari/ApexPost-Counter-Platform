namespace ApexPost.Web.Models;

public sealed record ShippingLabelViewModel(
	Guid TransactionId,
	TransactionPaymentStatus PaymentStatus,
	string OriginBranchCode,
	DateTimeOffset? OccurredAtUtc,
	IReadOnlyList<ShippingLabelItemViewModel> Items);

// ShipmentId is the ID that the partner parcel-status API looks up.
public sealed record ShippingLabelItemViewModel(
	string ShipmentId,
	ShippingItemType Type,
	ShippingServiceLevel ServiceLevel,
	decimal WeightKilograms,
	decimal LengthCm,
	decimal WidthCm,
	decimal HeightCm,
	string DestinationZone);
