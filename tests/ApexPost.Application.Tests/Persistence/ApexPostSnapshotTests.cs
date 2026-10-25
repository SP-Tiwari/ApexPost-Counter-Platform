
namespace ApexPost.Application.Tests.Persistence;

public class ApexPostSnapshotTests
{
	[Fact]
	public void Constructor_CopiesCollectionsToPreventCallerMutation()
	{
		var branches = new List<Branch>
		{
			new(
				new BranchCode("DEL01"),
				"Delhi",
				"North",
				"09:00-17:00",
				[new CounterTerminal("COUNTER01", "Counter 1")])
		};

		var snapshot = new ApexPostSnapshot(
			branches, [], [], [], [], [], [], [], []);
		branches.Clear();

		Assert.Single(snapshot.Branches);
	}

	[Fact]
	public void Constructor_RejectsNullCollectionsAndEntries()
	{
		Assert.Throws<ArgumentNullException>(() =>
			new ApexPostSnapshot(null!, [], [], [], [], [], [], [], []));
		Assert.Throws<ArgumentException>(() =>
			new ApexPostSnapshot(
				[null!], [], [], [], [], [], [], [], []));
	}
}
