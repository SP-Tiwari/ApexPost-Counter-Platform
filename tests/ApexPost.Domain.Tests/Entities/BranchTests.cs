

namespace ApexPost.Domain.Tests.Entities;

public class BranchTests
{
	[Fact]
	public void Constructor_WithValidDetails_CreatesActiveBranch()
	{
		var branch = new Branch(
			new BranchCode("DEL01"),
			"Delhi Main",
			"North",
			"09:00-18:00",
			[
				new CounterTerminal("C1", "Counter-1"),
				new CounterTerminal("C2", "Counter-2")
			]);

		Assert.Equal("DEL01", branch.Code.Value);
		Assert.Equal("Delhi Main", branch.Name);
		Assert.Equal("North", branch.Region);
		Assert.Equal("09:00-18:00", branch.OpeningHours);
		Assert.True(branch.IsActive);
		Assert.Equal(2, branch.CounterTerminals.Count);
	}

	[Fact]
	public void Close_SetsBranchInactive()
	{
		var branch = CreateBranch();

		branch.Close();

		Assert.False(branch.IsActive);
	}

	[Fact]
	public void Reopen_SetsBranchActive()
	{
		var branch = CreateBranch();
		branch.Close();

		branch.Reopen();

		Assert.True(branch.IsActive);
	}

	[Fact]
	public void Constructor_WithoutCounterTerminals_ThrowsArgumentException()
	{
		Assert.Throws<ArgumentException>(() =>
			new Branch(
				new BranchCode("DEL01"),
				"Delhi Main",
				"North",
				"09:00-18:00",
				[]));
	}

	[Fact]
	public void Constructor_WithEmptyName_ThrowsArgumentException()
	{
		Assert.Throws<ArgumentException>(() =>
			new Branch(
				new BranchCode("DEL01"),
				" ",
				"North",
				"09:00-18:00",
				[new CounterTerminal("C1", "Counter-1")]));
	}

	[Fact]
	public void Constructor_WithDuplicateTerminalIds_ThrowsArgumentException()
	{
		Assert.Throws<ArgumentException>(() =>
			new Branch(
				new BranchCode("DEL01"),
				"Delhi Main",
				"North",
				"09:00-18:00",
				[
					new CounterTerminal("C1", "Counter-1"),
					new CounterTerminal("c1", "Counter-2")
				]));
	}

	private static Branch CreateBranch() =>
		new(
			new BranchCode("DEL01"),
			"Delhi Main",
			"North",
			"09:00-18:00",
			[new CounterTerminal("C1", "Counter-1")]);
}
