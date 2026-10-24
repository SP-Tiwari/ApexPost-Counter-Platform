namespace ApexPost.Application.Persistence;

public interface IApexPostRepository
{
	Task<ApexPostSnapshot> LoadAsync(
		CancellationToken cancellationToken = default);

	Task SaveAsync(
		ApexPostSnapshot snapshot,
		CancellationToken cancellationToken = default);

	Task UpdateAsync(
		Func<ApexPostSnapshot, ApexPostSnapshot> update,
		CancellationToken cancellationToken = default);
}
