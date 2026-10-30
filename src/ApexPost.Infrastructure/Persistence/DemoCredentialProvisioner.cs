using ApexPost.Application.Persistence;
using ApexPost.Application.Security;

namespace ApexPost.Infrastructure.Persistence;

// Gives the seeded demo staff a sign-in credential using a password supplied
// by the operator (user-secrets in Development). The password itself is never
// stored; only PBKDF2 hashes are written to the separate credential file, and
// staff who already have a credential keep it.
public sealed class DemoCredentialProvisioner(
	IApexPostRepository repository,
	IStaffCredentialProvisioning credentials)
{
	// Returns the number of demo logins added.
	public async Task<int> ProvisionAsync(
		string password,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(password);

		var snapshot = await repository.LoadAsync(cancellationToken)
			.ConfigureAwait(false);
		var provisioned = await credentials.GetStaffIdsAsync(cancellationToken)
			.ConfigureAwait(false);
		var demoStaffIds = snapshot.Staff
			.Where(staff => staff.IsActive &&
				!provisioned.Contains(staff.Id) &&
				DemoDataInitializer.DemoStaffIds.Contains(staff.Id, StringComparer.Ordinal))
			.Select(staff => staff.Id)
			.ToArray();
		if (demoStaffIds.Length == 0)
			return 0;

		// PBKDF2 at the supported iteration count is CPU-bound; hash in parallel.
		var created = await Task.WhenAll(demoStaffIds.Select(staffId =>
			Task.Run(
				() => StaffPasswordHasher.CreateCredential(staffId, password),
				cancellationToken))).ConfigureAwait(false);

		return await credentials.AddMissingAsync(created, cancellationToken)
			.ConfigureAwait(false);
	}
}
