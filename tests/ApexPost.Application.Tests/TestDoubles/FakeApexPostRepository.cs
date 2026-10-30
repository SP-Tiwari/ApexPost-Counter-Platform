namespace ApexPost.Application.Tests.TestDoubles;

// Hand-written repository fake (the assignment allows only xUnit and
// Microsoft packages, so no mocking library). Counts calls so tests can
// check that a request was denied before any data was loaded.
internal sealed class FakeApexPostRepository(ApexPostSnapshot? snapshot = null)
	: IApexPostRepository
{
	private ApexPostSnapshot _snapshot = snapshot ?? Empty;

	public static ApexPostSnapshot Empty { get; } =
		new([], [], [], [], [], [], [], [], []);

	public int LoadCount { get; private set; }
	public int WriteCount { get; private set; }
	public ApexPostSnapshot Current => _snapshot;

	public Task<ApexPostSnapshot> LoadAsync(
		CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		LoadCount++;
		return Task.FromResult(_snapshot);
	}

	public Task SaveAsync(
		ApexPostSnapshot snapshot,
		CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		WriteCount++;
		_snapshot = snapshot;
		return Task.CompletedTask;
	}

	public Task UpdateAsync(
		Func<ApexPostSnapshot, ApexPostSnapshot> update,
		CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		WriteCount++;
		_snapshot = update(_snapshot);
		return Task.CompletedTask;
	}
}
