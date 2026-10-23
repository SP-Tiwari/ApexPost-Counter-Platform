
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Entities;

public sealed class Branch
{
	public BranchCode Code { get; }
	public string Name { get; }
	public string Region { get; }
	public string OpeningHours { get; }
	public bool IsActive { get; private set; }
	public IReadOnlyList<string> CounterTerminals => _counterTerminals.AsReadOnly();

	private readonly List<string> _counterTerminals = [];

	public Branch(
		BranchCode code,
		string name,
		string region,
		string openingHours,
		IEnumerable<string> counterTerminals)
	{
		ArgumentNullException.ThrowIfNull(code);
		ArgumentNullException.ThrowIfNull(counterTerminals);

		if (string.IsNullOrWhiteSpace(name))
			throw new ArgumentException("Branch name cannot be empty.", nameof(name));

		if (string.IsNullOrWhiteSpace(region))
			throw new ArgumentException("Branch region cannot be empty.", nameof(region));

		if (string.IsNullOrWhiteSpace(openingHours))
			throw new ArgumentException("Opening hours cannot be empty.", nameof(openingHours));

		var terminals = counterTerminals
			.Select(terminal => terminal?.Trim())
			.ToList();

		if (terminals.Count == 0 ||
			terminals.Any(string.IsNullOrWhiteSpace) ||
			terminals.Distinct(StringComparer.OrdinalIgnoreCase).Count() != terminals.Count)
		{
			throw new ArgumentException(
				"A branch must have at least one uniquely named counter terminal.",
				nameof(counterTerminals));
		}

		Code = code;
		Name = name.Trim();
		Region = region.Trim();
		OpeningHours = openingHours.Trim();
		IsActive = true;
		_counterTerminals.AddRange(terminals!);
	}

	public void Close() => IsActive = false;

	public void Reopen() => IsActive = true;
}
