using ApexPost.Application.Persistence;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Infrastructure.Persistence;

public sealed class DemoDataInitializer(IApexPostRepository repository)
{
	public async Task<bool> InitializeAsync(
		CancellationToken cancellationToken = default)
	{
		var initialized = false;
		await repository.UpdateAsync(snapshot =>
		{
			if (!snapshot.IsCompletelyEmpty)
				return snapshot;

			initialized = true;
			return CreateDemoSnapshot();
		}, cancellationToken).ConfigureAwait(false);

		return initialized;
	}

	private static ApexPostSnapshot CreateDemoSnapshot()
	{
		var branches = Enumerable.Range(1, 5)
			.Select(index => new Branch(
				new BranchCode($"DM{index:00}"),
				$"DEMO Branch {index:00}",
				$"DEMO Region {index:00}",
				"Demo hours only",
				[
					new CounterTerminal($"DEMO-T{index:00}-01", "DEMO Counter 1"),
					new CounterTerminal($"DEMO-T{index:00}-02", "DEMO Counter 2")
				]))
			.ToArray();

		var staff = Enumerable.Range(1, 20)
			.Select(index =>
			{
				var branchIndex = (index - 1) / 4;
				var role = ((index - 1) % 4) switch
				{
					0 => StaffRole.CounterClerk,
					1 => StaffRole.BranchManager,
					2 => StaffRole.Auditor,
					_ => StaffRole.Administrator
				};
				return new Staff(
					$"demo-staff-{index:00}",
					$"DEMO Staff {index:00} (not provisioned)",
					$"demo.staff{index:00}@example.invalid",
					role,
					branches[branchIndex].Code);
			})
			.ToArray();

		var products = Enumerable.Range(1, 30)
			.Select(index => new Product(
				new Sku($"DEMO-SKU-{index:000}"),
				$"DEMO Product {index:000}",
				(ProductCategory)((index - 1) % Enum.GetValues<ProductCategory>().Length),
				new Money(10m + index, "INR"),
				TaxCategory.ZeroRated))
			.ToArray();

		var branchStocks = branches
			.SelectMany(branch => products.Select(product =>
				new BranchStock(branch.Code, product.Sku, 10)))
			.ToArray();

		var shippingRates = Enumerable.Range(1, 10)
			.Select(index => new ShippingRate(
				$"DEMO-ZONE-{index:00}",
				ShippingServiceLevel.Standard,
				new Money(10m + index, "INR"),
				new Money(1m, "INR")))
			.ToArray();

		var taxRates = new Dictionary<TaxCategory, decimal>
		{
			[TaxCategory.Standard] = 0m,
			[TaxCategory.Reduced] = 0m,
			[TaxCategory.ZeroRated] = 0m
		};
		var transactions = Enumerable.Range(1, 40)
			.Select(index =>
			{
				var product = products[(index - 1) % products.Length];
				var basket = new TransactionBasket(
					Guid.Parse($"00000000-0000-0000-0000-{index:000000000000}"),
					"INR",
					taxRates,
					branches[(index - 1) % branches.Length].Code);
				basket.AddLineItem(new TransactionLineItem(product, 1));
				basket.Pay();
				return basket;
			})
			.ToArray();

		return new ApexPostSnapshot(
			branches,
			staff,
			products,
			branchStocks,
			[],
			shippingRates,
			[],
			[],
			transactions);
	}
}

internal static class ApexPostSnapshotExtensions
{
	extension(ApexPostSnapshot snapshot)
	{
		public bool IsCompletelyEmpty =>
			snapshot.Branches.Count == 0 &&
			snapshot.Staff.Count == 0 &&
			snapshot.Products.Count == 0 &&
			snapshot.BranchStocks.Count == 0 &&
			snapshot.ShippingItems.Count == 0 &&
			snapshot.ShippingRates.Count == 0 &&
			snapshot.MoneyOrders.Count == 0 &&
			snapshot.IdentityVerifications.Count == 0 &&
			snapshot.TransactionBaskets.Count == 0;
	}
}
