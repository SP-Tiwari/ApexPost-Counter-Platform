
namespace ApexPost.Domain.Entities;

public sealed class CounterTerminal
{
	public string Id { get; }
	public string Name { get; }
	public bool IsActive { get; private set; }

	public CounterTerminal(string id, string name)
	{
		if (string.IsNullOrWhiteSpace(id))
			throw new ArgumentException("Counter terminal ID cannot be empty.", nameof(id));

		if (string.IsNullOrWhiteSpace(name))
			throw new ArgumentException("Counter terminal name cannot be empty.", nameof(name));

		Id = id.Trim();
		Name = name.Trim();
		IsActive = true;
	}

	public void Deactivate() => IsActive = false;

	public void Activate() => IsActive = true;
}
