using ApexPost.Application.Caching;
using ApexPost.Domain.Specifications;

namespace ApexPost.Application.Services;

// QuantityOnHand is null when the branch holds no stock record for the
// product, which is different from a record of zero.
public sealed record ProductCatalogueRow(
	Product Product,
	int? QuantityOnHand);

public sealed record ProductCatalogue(
	Branch Branch,
	IReadOnlyList<ProductCatalogueRow> Rows,
	DateTimeOffset CatalogueLoadedAtUtc);

// The product catalogue with stock on hand at the signed-in staff member's
// home branch. Products come from the reference-data cache; stock levels
// change with every sale, so they are always read from the repository.
public sealed class ProductCatalogueApplicationService
{
	private readonly IApexPostRepository _repository;
	private readonly ReferenceDataCache _referenceData;

	public ProductCatalogueApplicationService(
		IApexPostRepository repository,
		ReferenceDataCache referenceData)
	{
		ArgumentNullException.ThrowIfNull(repository);
		ArgumentNullException.ThrowIfNull(referenceData);
		_repository = repository;
		_referenceData = referenceData;
	}

	public async Task<ProductCatalogue> GetCatalogueAsync(
		string staffId,
		CancellationToken cancellationToken = default)
	{
		// Independent reads, so they run in parallel; a cache hit completes
		// without touching the store.
		var snapshotTask = _repository.LoadAsync(cancellationToken);
		var referenceTask = _referenceData.GetAsync(cancellationToken).AsTask();
		await Task.WhenAll(snapshotTask, referenceTask).ConfigureAwait(false);
		var snapshot = await snapshotTask.ConfigureAwait(false);
		var referenceData = await referenceTask.ConfigureAwait(false);

		var staff = FindActiveStaff(snapshot, staffId);
		var branch = snapshot.Branches.SingleOrDefault(
			candidate => candidate.Code == staff.HomeBranchCode)
			?? throw new KeyNotFoundException(
				$"Branch '{staff.HomeBranchCode}' was not found.");

		var branchStocks = snapshot.BranchStocks
			.Matching(new StockAtBranchSpecification(branch.Code))
			.ToArray();
		if (branchStocks.Select(stock => stock.ProductSku).Distinct().Count() !=
			branchStocks.Length)
		{
			throw new InvalidOperationException(
				$"Multiple stock records match one product at branch '{branch.Code}'.");
		}
		var quantities = branchStocks.ToDictionary(
			stock => stock.ProductSku, stock => stock.Quantity);
		var rows = referenceData.Products
			.OrderBy(product => product.Category)
			.ThenBy(product => product.Name, StringComparer.CurrentCultureIgnoreCase)
			.Select(product => new ProductCatalogueRow(
				product,
				quantities.TryGetValue(product.Sku, out var quantity)
					? quantity
					: null))
			.ToArray();

		return new ProductCatalogue(
			branch, Array.AsReadOnly(rows), referenceData.LoadedAtUtc);
	}

	private static Staff FindActiveStaff(ApexPostSnapshot snapshot, string staffId)
	{
		if (string.IsNullOrWhiteSpace(staffId))
			throw new UnauthorizedAccessException(
				"Active staff are required to view the catalogue.");

		var matches = snapshot.Staff
			.Where(staff => string.Equals(
				staff.Id, staffId, StringComparison.Ordinal))
			.Take(2)
			.ToArray();
		if (matches.Length != 1 || !matches[0].IsActive)
			throw new UnauthorizedAccessException(
				"Active staff are required to view the catalogue.");

		return matches[0];
	}
}
