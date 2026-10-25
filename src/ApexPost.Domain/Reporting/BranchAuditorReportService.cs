
namespace ApexPost.Domain.Reporting;

public sealed record BranchAuditorReportEntry
{
	public BranchCode BranchCode { get; }
	public MoneyOrder? MoneyOrder { get; }
	public IdentityVerification? IdentityVerification { get; }

	public BranchAuditorReportEntry(
		BranchCode branchCode,
		MoneyOrder? moneyOrder,
		IdentityVerification? identityVerification)
	{
		ArgumentNullException.ThrowIfNull(branchCode);

		if (moneyOrder is null && identityVerification is null)
			throw new ArgumentException(
				"An auditor report entry must contain a money order or identity verification.");

		BranchCode = branchCode;
		MoneyOrder = moneyOrder;
		IdentityVerification = identityVerification;
	}
}

public sealed record BranchAuditorReportRow(
	BranchCode BranchCode,
	int MoneyOrderCount,
	int IdentityVerificationCount,
	int SuccessfulIdentityVerificationCount,
	int FailedIdentityVerificationCount);

public sealed class BranchAuditorReportService
{
	public IReadOnlyList<BranchAuditorReportRow> CreateReport(
		IEnumerable<BranchAuditorReportEntry> entries)
	{
		ArgumentNullException.ThrowIfNull(entries);

		var totals = new Dictionary<BranchCode, (int Orders, int Successful, int Failed)>();
		foreach (var entry in entries)
		{
			ArgumentNullException.ThrowIfNull(entry);

			var current = totals.GetValueOrDefault(entry.BranchCode);
			totals[entry.BranchCode] = (
				checked(current.Orders + (entry.MoneyOrder is null ? 0 : 1)),
				checked(current.Successful
					+ (entry.IdentityVerification?.IsSuccessful == true ? 1 : 0)),
				checked(current.Failed
					+ (entry.IdentityVerification is { IsSuccessful: false } ? 1 : 0)));
		}

		return totals
			.Select(pair => new BranchAuditorReportRow(
				pair.Key,
				pair.Value.Orders,
				checked(pair.Value.Successful + pair.Value.Failed),
				pair.Value.Successful,
				pair.Value.Failed))
			.OrderBy(row => row.BranchCode.Value, StringComparer.Ordinal)
			.ToArray();
	}
}
