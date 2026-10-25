

namespace ApexPost.Domain.Tests.Entities;

public class StaffTests
{
	[Fact]
	public void Constructor_WithValidDetails_CreatesActiveStaff()
	{
		var staff = CreateStaff();

		Assert.Equal("S001", staff.Id);
		Assert.Equal("Asha Kumar", staff.Name);
		Assert.Equal("asha@example.com", staff.Email);
		Assert.Equal(StaffRole.CounterClerk, staff.Role);
		Assert.Equal("DEL01", staff.HomeBranchCode.Value);
		Assert.True(staff.IsActive);
	}

	[Fact]
	public void Constructor_WithEmptyName_ThrowsArgumentException()
	{
		Assert.Throws<ArgumentException>(() =>
			new Staff("S001", " ", "asha@example.com",
				StaffRole.CounterClerk, new BranchCode("DEL01")));
	}

	[Fact]
	public void Constructor_WithInvalidEmail_ThrowsArgumentException()
	{
		Assert.Throws<ArgumentException>(() =>
			new Staff("S001", "Asha Kumar", "invalid-email",
				StaffRole.CounterClerk, new BranchCode("DEL01")));
	}

	[Fact]
	public void UpdateRole_ChangesStaffRole()
	{
		var staff = CreateStaff();

		staff.UpdateRole(StaffRole.BranchManager);

		Assert.Equal(StaffRole.BranchManager, staff.Role);
	}

	[Fact]
	public void AssignToBranch_ChangesHomeBranch()
	{
		var staff = CreateStaff();

		staff.AssignToBranch(new BranchCode("MUM01"));

		Assert.Equal("MUM01", staff.HomeBranchCode.Value);
	}

	[Fact]
	public void Deactivate_SetsStaffInactive()
	{
		var staff = CreateStaff();

		staff.Deactivate();

		Assert.False(staff.IsActive);
	}

	[Fact]
	public void Activate_SetsStaffActive()
	{
		var staff = CreateStaff();
		staff.Deactivate();

		staff.Activate();

		Assert.True(staff.IsActive);
	}

	private static Staff CreateStaff() =>
		new("S001", "Asha Kumar", "asha@example.com",
			StaffRole.CounterClerk, new BranchCode("DEL01"));
}
