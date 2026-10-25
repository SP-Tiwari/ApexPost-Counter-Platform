

namespace ApexPost.Domain.Tests.Entities;

public class CounterTerminalTests
{
	[Fact]
	public void Constructor_WithValidDetails_CreatesActiveTerminal()
	{
		var terminal = new CounterTerminal("C1", "Counter-1");

		Assert.Equal("C1", terminal.Id);
		Assert.Equal("Counter-1", terminal.Name);
		Assert.True(terminal.IsActive);
	}

	[Fact]
	public void Constructor_WithEmptyId_ThrowsArgumentException()
	{
		Assert.Throws<ArgumentException>(() =>
			new CounterTerminal(" ", "Counter-1"));
	}

	[Fact]
	public void Constructor_WithEmptyName_ThrowsArgumentException()
	{
		Assert.Throws<ArgumentException>(() =>
			new CounterTerminal("C1", " "));
	}

	[Fact]
	public void Deactivate_SetsTerminalInactive()
	{
		var terminal = new CounterTerminal("C1", "Counter-1");

		terminal.Deactivate();

		Assert.False(terminal.IsActive);
	}

	[Fact]
	public void Activate_SetsTerminalActive()
	{
		var terminal = new CounterTerminal("C1", "Counter-1");
		terminal.Deactivate();

		terminal.Activate();

		Assert.True(terminal.IsActive);
	}
}
