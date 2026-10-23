
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Tests.ValueObjects;

public class BranchCodeTests
{
	[Fact]
	public void Constructor_WithValidCode_CreatesBranchCode()
	{
		var branchCode = new BranchCode("DEL01");

		Assert.Equal("DEL01", branchCode.Value);
	}

	[Fact]
	public void Constructor_WithLowercaseCode_ConvertsToUppercase()
	{
		var branchCode = new BranchCode("del01");

		Assert.Equal("DEL01", branchCode.Value);
	}

	[Fact]
	public void Constructor_WithWhitespace_TrimsCode()
	{
		var branchCode = new BranchCode(" del01 ");

		Assert.Equal("DEL01", branchCode.Value);
	}

	[Theory]
	[InlineData("")]
	[InlineData(" ")]
	[InlineData(null)]
	public void Constructor_WithEmptyCode_ThrowsException(string? value)
	{
		Assert.Throws<ArgumentException>(
			() => new BranchCode(value!));
	}

	[Theory]
	[InlineData("A")]
	[InlineData("ABCDEFGHIJK")]
	public void Constructor_WithInvalidLength_ThrowsException(string value)
	{
		Assert.Throws<ArgumentException>(
			() => new BranchCode(value));
	}

	[Fact]
	public void ToString_ReturnsBranchCodeValue()
	{
		var branchCode = new BranchCode("DEL01");

		Assert.Equal("DEL01", branchCode.ToString());
	}
}
