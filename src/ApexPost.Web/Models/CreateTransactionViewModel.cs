using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Web.Models;

public sealed class CreateTransactionViewModel
{
	public Guid TransactionId { get; set; }

	public string? GoodsSku { get; set; }
	public int? GoodsQuantity { get; set; }
	public bool IncludeShipping { get; set; }
	public ShippingItemType? ShippingType { get; set; }
	public decimal? WeightKilograms { get; set; }
	public decimal? LengthCm { get; set; }
	public decimal? WidthCm { get; set; }
	public decimal? HeightCm { get; set; }
	public string? DestinationZone { get; set; }
	public ShippingServiceLevel? ServiceLevel { get; set; }

	public IReadOnlyList<Product> Products { get; set; } = [];
	public Branch? Branch { get; set; }
	public IReadOnlyList<ShippingRate> ShippingRates { get; set; } = [];
	public bool CanSubmit { get; set; }
}
