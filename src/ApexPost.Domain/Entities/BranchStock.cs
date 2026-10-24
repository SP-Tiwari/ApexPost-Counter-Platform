
using ApexPost.Domain;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Entities;

public sealed class BranchStock
{
	public BranchCode BranchCode { get; }
	public Sku ProductSku { get; }
	private int _quantity;

	public int Quantity
	{
		get
		{
			lock (DomainMutationLock.Gate)
				return _quantity;
		}
		private set => _quantity = value;
	}

	public BranchStock(BranchCode branchCode, Sku productSku, int quantity)
	{
		ArgumentNullException.ThrowIfNull(branchCode);
		ArgumentNullException.ThrowIfNull(productSku);

		if (quantity < 0)
			throw new ArgumentOutOfRangeException(
				nameof(quantity), "Stock quantity cannot be negative.");

		BranchCode = branchCode;
		ProductSku = productSku;
		Quantity = quantity;
	}

	public void AddStock(int quantity)
	{
		lock (DomainMutationLock.Gate)
		{
			if (quantity <= 0)
				throw new ArgumentOutOfRangeException(
					nameof(quantity), "Quantity must be greater than zero.");

			Quantity = checked(Quantity + quantity);
		}
	}

	public void RemoveStock(int quantity)
	{
		lock (DomainMutationLock.Gate)
		{
			if (quantity <= 0)
				throw new ArgumentOutOfRangeException(
					nameof(quantity), "Quantity must be greater than zero.");

			if (quantity > Quantity)
				throw new InvalidOperationException(
					"Insufficient stock available.");

			Quantity -= quantity;
		}
	}
}
