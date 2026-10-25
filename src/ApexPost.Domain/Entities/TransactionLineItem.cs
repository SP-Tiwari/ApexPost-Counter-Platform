using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Entities;

public sealed class TransactionLineItem
{
	public string Description { get; }
	public TransactionItemType Type { get; }
	public Money UnitPrice { get; }
	public int Quantity { get; }
	public TaxCategory TaxCategory { get; }
	public Sku? ProductSku { get; }
	public ShippingItem? ShippingItem { get; }

	public TransactionLineItem(
		string description,
		TransactionItemType type,
		Money unitPrice,
		int quantity,
		TaxCategory taxCategory)
		: this(description, type, unitPrice, quantity, taxCategory, null, null)
	{
	}

	public TransactionLineItem(
		string description,
		TransactionItemType type,
		Money unitPrice,
		int quantity,
		TaxCategory taxCategory,
		Sku? productSku,
		ShippingItem? shippingItem = null)
	{
		if (string.IsNullOrWhiteSpace(description))
			throw new ArgumentException(
				"Transaction line description is required.", nameof(description));

		if (!Enum.IsDefined(type))
			throw new ArgumentOutOfRangeException(nameof(type));

		ArgumentNullException.ThrowIfNull(unitPrice);

		if (quantity <= 0)
			throw new ArgumentOutOfRangeException(
				nameof(quantity), "Transaction line quantity must be greater than zero.");

		if (!Enum.IsDefined(taxCategory))
			throw new ArgumentOutOfRangeException(nameof(taxCategory));

		Description = description.Trim();
		Type = type;
		UnitPrice = unitPrice;
		Quantity = quantity;
		TaxCategory = taxCategory;
		ProductSku = productSku;
		if (shippingItem is not null && type != TransactionItemType.Service)
			throw new ArgumentException(
				"Shipping items must be represented by a service line.",
				nameof(shippingItem));
		ShippingItem = shippingItem;
	}

	public TransactionLineItem(Product product, int quantity)
	{
		ArgumentNullException.ThrowIfNull(product);

		if (quantity <= 0)
			throw new ArgumentOutOfRangeException(
				nameof(quantity), "Transaction line quantity must be greater than zero.");

		Description = product.Name;
		Type = TransactionItemType.Goods;
		UnitPrice = product.UnitPrice;
		Quantity = quantity;
		TaxCategory = product.TaxCategory;
		ProductSku = product.Sku;
		ShippingItem = null;
	}

	public Money Subtotal =>
		new(UnitPrice.Amount * Quantity, UnitPrice.Currency);
}
