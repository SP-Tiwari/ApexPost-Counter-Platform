namespace ApexPost.Application.Services;

/// <summary>
/// Administrative master-data use cases.  The Web layer deliberately supplies only
/// the acting staff ID; authorization is rechecked against persisted staff data.
/// </summary>
public sealed class AdministrationApplicationService(IApexPostRepository repository)
{
	private readonly IApexPostRepository _repository = repository ??
		throw new ArgumentNullException(nameof(repository));

	public async Task<ApexPostSnapshot> GetSnapshotAsync(
		string administratorId, CancellationToken cancellationToken = default)
	{
		var snapshot = await _repository.LoadAsync(cancellationToken).ConfigureAwait(false);
		EnsureAdministrator(snapshot, administratorId);
		return snapshot;
	}

	public Task AddStaffAsync(string administratorId, string id, string name,
		string email, StaffRole role, string homeBranchCode,
		CancellationToken cancellationToken = default) =>
		_repository.UpdateAsync(snapshot =>
		{
			EnsureAdministrator(snapshot, administratorId);
			var branchCode = new BranchCode(homeBranchCode);
			EnsureExists(snapshot.Branches, branch => branch.Code == branchCode,
				$"Branch '{branchCode}'");
			EnsureMissing(snapshot.Staff, staff =>
				string.Equals(staff.Id, id?.Trim(), StringComparison.Ordinal), "Staff ID");
			EnsureMissing(snapshot.Staff, staff =>
				string.Equals(staff.Email, email?.Trim(), StringComparison.OrdinalIgnoreCase), "Staff email");
			return Copy(snapshot, staff: snapshot.Staff.Append(new Staff(id, name, email, role, branchCode)));
		}, cancellationToken);

	public Task UpdateStaffAsync(string administratorId, string staffId, StaffRole role,
		string homeBranchCode, bool isActive, CancellationToken cancellationToken = default) =>
		_repository.UpdateAsync(snapshot =>
		{
			EnsureAdministrator(snapshot, administratorId);
			var staff = Find(snapshot.Staff, entry => string.Equals(entry.Id, staffId, StringComparison.Ordinal), $"Staff '{staffId}'");
			var branchCode = new BranchCode(homeBranchCode);
			EnsureExists(snapshot.Branches, branch => branch.Code == branchCode, $"Branch '{branchCode}'");
			staff.UpdateRole(role);
			staff.AssignToBranch(branchCode);
			if (isActive) staff.Activate(); else staff.Deactivate();
			return snapshot;
		}, cancellationToken);

	public Task AddBranchAsync(string administratorId, string code, string name,
		string region, string openingHours, string terminalId, string terminalName,
		CancellationToken cancellationToken = default) =>
		_repository.UpdateAsync(snapshot =>
		{
			EnsureAdministrator(snapshot, administratorId);
			var branchCode = new BranchCode(code);
			EnsureMissing(snapshot.Branches, branch => branch.Code == branchCode, "Branch code");
			var branch = new Branch(branchCode, name, region, openingHours,
				[new CounterTerminal(terminalId, terminalName)]);
			return Copy(snapshot, branches: snapshot.Branches.Append(branch));
		}, cancellationToken);

	public Task SetBranchStatusAsync(string administratorId, string code, bool isActive,
		CancellationToken cancellationToken = default) =>
		_repository.UpdateAsync(snapshot =>
		{
			EnsureAdministrator(snapshot, administratorId);
			var branchCode = new BranchCode(code);
			var branch = Find(snapshot.Branches, entry => entry.Code == branchCode, $"Branch '{branchCode}'");
			if (isActive) branch.Reopen(); else branch.Close();
			return snapshot;
		}, cancellationToken);

	public Task AddProductAsync(string administratorId, string sku, string name,
		ProductCategory category, decimal unitPrice, string currency, TaxCategory taxCategory,
		CancellationToken cancellationToken = default) =>
		_repository.UpdateAsync(snapshot =>
		{
			EnsureAdministrator(snapshot, administratorId);
			var productSku = new Sku(sku);
			EnsureMissing(snapshot.Products, product => product.Sku == productSku, "Product SKU");
			var product = new Product(productSku, name, category,
				new Money(unitPrice, currency), taxCategory);
			return Copy(snapshot, products: snapshot.Products.Append(product));
		}, cancellationToken);

	private static void EnsureAdministrator(ApexPostSnapshot snapshot, string staffId)
	{
		var administrator = Find(snapshot.Staff, staff => string.Equals(staff.Id, staffId, StringComparison.Ordinal), "Administrator");
		if (!administrator.IsActive || administrator.Role != StaffRole.Administrator)
			throw new UnauthorizedAccessException("An active Administrator is required.");
	}

	private static T Find<T>(IEnumerable<T> values, Func<T, bool> predicate, string description)
	{
		var entries = values.Where(predicate).Take(2).ToArray();
		return entries.Length switch
		{
			1 => entries[0],
			0 => throw new KeyNotFoundException($"{description} was not found."),
			_ => throw new InvalidOperationException($"Multiple records match {description}.")
		};
	}

	private static void EnsureExists<T>(IEnumerable<T> values, Func<T, bool> predicate, string description) =>
		_ = Find(values, predicate, description);
	private static void EnsureMissing<T>(IEnumerable<T> values, Func<T, bool> predicate, string description)
	{
		if (values.Any(predicate)) throw new InvalidOperationException($"{description} already exists.");
	}

	private static ApexPostSnapshot Copy(ApexPostSnapshot source,
		IEnumerable<Branch>? branches = null, IEnumerable<Staff>? staff = null,
		IEnumerable<Product>? products = null) => new(
		branches ?? source.Branches, staff ?? source.Staff, products ?? source.Products,
		source.BranchStocks, source.ShippingItems, source.ShippingRates,
		source.MoneyOrders, source.IdentityVerifications, source.TransactionBaskets);
}
