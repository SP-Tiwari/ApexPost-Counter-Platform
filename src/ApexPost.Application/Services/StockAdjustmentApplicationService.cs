namespace ApexPost.Application.Services;

public sealed record StockAdjustmentCatalog(
	Branch Branch,
	IReadOnlyList<BranchStock> BranchStocks);

public sealed class StockAdjustmentApplicationService
{
	private readonly IApexPostRepository _repository;

	public StockAdjustmentApplicationService(IApexPostRepository repository)
	{
		ArgumentNullException.ThrowIfNull(repository);
		_repository = repository;
	}

	public async Task<StockAdjustmentCatalog> GetCatalogAsync(
		string staffId,
		CancellationToken cancellationToken = default)
	{
		var snapshot = await _repository.LoadAsync(cancellationToken)
			.ConfigureAwait(false);
		var staff = FindActiveBranchManager(snapshot, staffId);
		var branch = FindBranch(snapshot, staff.HomeBranchCode);
		var branchStocks = snapshot.BranchStocks
			.Where(stock => stock.BranchCode == staff.HomeBranchCode)
			.ToArray();
		return new StockAdjustmentCatalog(
			branch, Array.AsReadOnly(branchStocks));
	}

	public Task AdjustStockAsync(
		string staffId,
		string productSku,
		int quantityChange,
		CancellationToken cancellationToken = default) =>
		_repository.UpdateAsync(snapshot =>
		{
			var staff = FindActiveBranchManager(snapshot, staffId);
			_ = FindBranch(snapshot, staff.HomeBranchCode);

			if (quantityChange == 0 || quantityChange == int.MinValue)
				throw new ArgumentOutOfRangeException(
					nameof(quantityChange),
					"Stock adjustment must be a non-zero supported quantity.");

			var sku = new Sku(productSku);
			_ = FindExactly(
				snapshot.Products,
				product => product.Sku == sku,
				$"Product '{sku}'");
			var stock = FindExactly(
				snapshot.BranchStocks,
				candidate =>
					candidate.BranchCode == staff.HomeBranchCode &&
					candidate.ProductSku == sku,
				$"Stock for product '{sku}' at branch '{staff.HomeBranchCode}'");

			if (quantityChange > 0)
				stock.AddStock(quantityChange);
			else
				stock.RemoveStock(-quantityChange);

			return snapshot;
		}, cancellationToken);

	private static Staff FindActiveBranchManager(
		ApexPostSnapshot snapshot,
		string staffId)
	{
		if (string.IsNullOrWhiteSpace(staffId))
			throw new UnauthorizedAccessException(
				"An active Branch Manager is required to adjust stock.");

		var matches = snapshot.Staff
			.Where(staff => string.Equals(
				staff.Id, staffId, StringComparison.Ordinal))
			.Take(2)
			.ToArray();
		if (matches.Length != 1 ||
			!matches[0].IsActive ||
			matches[0].Role != StaffRole.BranchManager)
		{
			throw new UnauthorizedAccessException(
				"An active Branch Manager is required to adjust stock.");
		}

		return matches[0];
	}

	private static Branch FindBranch(
		ApexPostSnapshot snapshot,
		BranchCode branchCode) =>
		FindExactly(
			snapshot.Branches,
			branch => branch.Code == branchCode,
			$"Branch '{branchCode}'");

	private static T FindExactly<T>(
		IEnumerable<T> entries,
		Func<T, bool> predicate,
		string description)
	{
		var matches = entries.Where(predicate).Take(2).ToArray();
		return matches.Length switch
		{
			0 => throw new KeyNotFoundException(
				$"{description} was not found."),
			1 => matches[0],
			_ => throw new InvalidOperationException(
				$"Multiple records match {description}.")
		};
	}
}
