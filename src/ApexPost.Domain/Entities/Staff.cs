

namespace ApexPost.Domain.Entities;

public sealed class Staff
{
	public string Id { get; }
	public string Name { get; }
	public string Email { get; }
	public StaffRole Role { get; private set; }
	public BranchCode HomeBranchCode { get; private set; }
	public bool IsActive { get; private set; }

	public Staff(
		string id,
		string name,
		string email,
		StaffRole role,
		BranchCode homeBranchCode)
	{
		if (string.IsNullOrWhiteSpace(id))
			throw new ArgumentException("Staff ID cannot be empty.", nameof(id));

		if (string.IsNullOrWhiteSpace(name))
			throw new ArgumentException("Staff name cannot be empty.", nameof(name));

		if (string.IsNullOrWhiteSpace(email) ||
			!email.Contains('@') ||
			email.StartsWith('@') ||
			email.EndsWith('@'))
		{
			throw new ArgumentException("Staff email is invalid.", nameof(email));
		}

		if (!Enum.IsDefined(role))
			throw new ArgumentOutOfRangeException(nameof(role), "Staff role is invalid.");

		ArgumentNullException.ThrowIfNull(homeBranchCode);

		Id = id.Trim();
		Name = name.Trim();
		Email = email.Trim();
		Role = role;
		HomeBranchCode = homeBranchCode;
		IsActive = true;
	}

	public void UpdateRole(StaffRole role)
	{
		if (!Enum.IsDefined(role))
			throw new ArgumentOutOfRangeException(nameof(role), "Staff role is invalid.");

		Role = role;
	}

	public void AssignToBranch(BranchCode branchCode)
	{
		ArgumentNullException.ThrowIfNull(branchCode);
		HomeBranchCode = branchCode;
	}

	public void Deactivate() => IsActive = false;

	public void Activate() => IsActive = true;
}
