
namespace ApexPost.Domain.Tests.ValueObjects;

public class IdentityVerificationTests
{
	[Fact]
	public void Constructor_WithValidDetails_StoresOnlyMaskedIdentifier()
	{
		const string documentIdentifier = "ABCD1234";
		var verification = new IdentityVerification(
			"Passport",
			documentIdentifier,
			true);

		Assert.Equal("Passport", verification.DocumentType);
		Assert.Equal("****1234", verification.MaskedDocumentIdentifier);
		Assert.True(verification.IsSuccessful);
		Assert.DoesNotContain(
			documentIdentifier,
			verification.MaskedDocumentIdentifier,
			StringComparison.Ordinal);
		Assert.DoesNotContain(
			documentIdentifier,
			verification.ToString(),
			StringComparison.Ordinal);
	}

	[Fact]
	public void Constructor_WithFiveCharacterIdentifier_MasksAllButLastFour()
	{
		var verification = new IdentityVerification("Passport", "A1234", false);

		Assert.Equal("*1234", verification.MaskedDocumentIdentifier);
		Assert.False(verification.IsSuccessful);
	}

	[Fact]
	public void Constructor_WithShortIdentifier_RejectsWithoutEchoingIdentifier()
	{
		const string shortIdentifier = "1234";

		var exception = Assert.Throws<ArgumentException>(() =>
			new IdentityVerification("Passport", shortIdentifier, true));

		Assert.DoesNotContain(shortIdentifier, exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void Constructor_WithControlCharacters_RejectsWithoutEchoingIdentifier()
	{
		const string invalidIdentifier = "ABC\n1234";

		var exception = Assert.Throws<ArgumentException>(() =>
			new IdentityVerification("Passport", invalidIdentifier, true));

		Assert.DoesNotContain(
			invalidIdentifier,
			exception.Message,
			StringComparison.Ordinal);
	}

	[Fact]
	public void Constructor_WithNullIdentifier_ThrowsArgumentNullException()
	{
		Assert.Throws<ArgumentNullException>(() =>
			new IdentityVerification("Passport", null!, true));
	}

	[Theory]
	[InlineData("")]
	[InlineData(" ")]
	public void Constructor_WithMissingDocumentType_ThrowsArgumentException(
		string documentType)
	{
		Assert.Throws<ArgumentException>(() =>
			new IdentityVerification(documentType, "ABCD1234", true));
	}
}
