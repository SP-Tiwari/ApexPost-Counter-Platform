namespace ApexPost.Application.Security;

public interface IStaffCredentialRepository
{
	Task<StaffCredential?> FindAsync(
		string staffId,
		CancellationToken cancellationToken = default);
}
