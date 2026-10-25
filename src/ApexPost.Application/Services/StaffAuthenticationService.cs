using ApexPost.Application.Security;

namespace ApexPost.Application.Services;

public sealed class StaffAuthenticationService
{
	private readonly IApexPostRepository _repository;
	private readonly IStaffCredentialRepository _credentials;

	public StaffAuthenticationService(
		IApexPostRepository repository,
		IStaffCredentialRepository credentials)
	{
		ArgumentNullException.ThrowIfNull(repository);
		ArgumentNullException.ThrowIfNull(credentials);
		_repository = repository;
		_credentials = credentials;
	}

	public async Task<Staff?> AuthenticateAsync(
		string staffId,
		string password,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(staffId) ||
			string.IsNullOrEmpty(password))
		{
			StaffPasswordHashVerifier.PerformDummyVerification(password ?? string.Empty);
			return null;
		}

		var credential = await _credentials.FindAsync(
			staffId.Trim(), cancellationToken).ConfigureAwait(false);
		if (credential is null)
		{
			StaffPasswordHashVerifier.PerformDummyVerification(password);
			return null;
		}

		if (!StaffPasswordHashVerifier.Verify(password, credential))
			return null;

		var snapshot = await _repository.LoadAsync(cancellationToken)
			.ConfigureAwait(false);
		var matches = snapshot.Staff
			.Where(staff => string.Equals(
				staff.Id, credential.StaffId, StringComparison.Ordinal))
			.Take(2)
			.ToArray();

		if (matches.Length > 1)
			throw new InvalidDataException(
				"Multiple staff records match the credential identity.");

		var staff = matches.SingleOrDefault();
		return staff is { IsActive: true } ? staff : null;
	}

	public async Task<Staff?> FindActiveStaffAsync(
		string staffId,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(staffId))
			return null;

		var snapshot = await _repository.LoadAsync(cancellationToken)
			.ConfigureAwait(false);
		var matches = snapshot.Staff
			.Where(staff => string.Equals(
				staff.Id, staffId, StringComparison.Ordinal))
			.Take(2)
			.ToArray();

		if (matches.Length > 1)
			throw new InvalidDataException(
				"Multiple staff records match the authenticated identity.");

		var staff = matches.SingleOrDefault();
		return staff is { IsActive: true } ? staff : null;
	}
}
