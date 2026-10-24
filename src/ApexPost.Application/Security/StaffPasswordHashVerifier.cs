using System.Security.Cryptography;

namespace ApexPost.Application.Security;

public static class StaffPasswordHashVerifier
{
	public const int CurrentVersion = 1;
	public const int DefaultIterations = 210_000;
	public const int SaltSizeBytes = 16;
	public const int HashSizeBytes = 32;

	public const int MinimumIterations = 600_000;
	private const int MaximumIterations = 2_000_000;
	private static readonly byte[] DummySalt = new byte[SaltSizeBytes];
	private static readonly byte[] DummyHash = new byte[HashSizeBytes];

	public static bool Verify(string password, StaffCredential credential)
	{
		ArgumentNullException.ThrowIfNull(password);
		Validate(credential);

		var salt = Convert.FromBase64String(credential.Salt);
		var expectedHash = Convert.FromBase64String(credential.PasswordHash);
		var actualHash = Rfc2898DeriveBytes.Pbkdf2(
			password,
			salt,
			credential.Iterations,
			HashAlgorithmName.SHA256,
			HashSizeBytes);
		return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
	}

	public static void Validate(StaffCredential credential)
	{
		ArgumentNullException.ThrowIfNull(credential);
		if (credential.Version != CurrentVersion ||
			credential.Iterations is < MinimumIterations or > MaximumIterations)
		{
			throw new InvalidDataException(
				"Staff credential uses unsupported password-hash parameters.");
		}

		byte[] salt;
		byte[] expectedHash;
		try
		{
			salt = Convert.FromBase64String(credential.Salt);
			expectedHash = Convert.FromBase64String(credential.PasswordHash);
		}
		catch (FormatException exception)
		{
			throw new InvalidDataException(
				"Staff credential contains invalid password-hash encoding.",
				exception);
		}

		if (salt.Length != SaltSizeBytes || expectedHash.Length != HashSizeBytes)
		{
			throw new InvalidDataException(
				"Staff credential contains invalid password-hash lengths.");
		}
	}

	public static void PerformDummyVerification(string password)
	{
		ArgumentNullException.ThrowIfNull(password);
		_ = Rfc2898DeriveBytes.Pbkdf2(
			password,
			DummySalt,
			DefaultIterations,
			HashAlgorithmName.SHA256,
			HashSizeBytes);
	}
}
