namespace ApexPost.Application.Security;

// Write side of the credential store, used only to provision credentials
// (for example demo logins in Development). Existing credentials are never
// replaced, so provisioning is idempotent and cannot reset a password.
public interface IStaffCredentialProvisioning
{
	// Staff IDs that already have a credential; empty when no store exists yet.
	Task<IReadOnlySet<string>> GetStaffIdsAsync(
		CancellationToken cancellationToken = default);

	// Returns the number of credentials added.
	Task<int> AddMissingAsync(
		IReadOnlyCollection<StaffCredential> credentials,
		CancellationToken cancellationToken = default);
}
