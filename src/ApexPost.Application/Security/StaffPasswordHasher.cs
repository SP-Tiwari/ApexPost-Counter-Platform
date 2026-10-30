using System.Security.Cryptography;

namespace ApexPost.Application.Security;

// Creates credentials that StaffPasswordHashVerifier accepts: PBKDF2-SHA256
// with a random salt and the minimum supported iteration count.
public static class StaffPasswordHasher
{
	public static StaffCredential CreateCredential(string staffId, string password)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(staffId);
		ArgumentException.ThrowIfNullOrWhiteSpace(password);

		var salt = RandomNumberGenerator.GetBytes(
			StaffPasswordHashVerifier.SaltSizeBytes);
		var hash = Rfc2898DeriveBytes.Pbkdf2(
			password,
			salt,
			StaffPasswordHashVerifier.MinimumIterations,
			HashAlgorithmName.SHA256,
			StaffPasswordHashVerifier.HashSizeBytes);

		return new StaffCredential(
			staffId.Trim(),
			StaffPasswordHashVerifier.CurrentVersion,
			StaffPasswordHashVerifier.MinimumIterations,
			Convert.ToBase64String(salt),
			Convert.ToBase64String(hash));
	}
}
