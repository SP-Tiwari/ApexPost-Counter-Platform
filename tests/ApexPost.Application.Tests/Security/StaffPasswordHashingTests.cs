using ApexPost.Application.Security;

namespace ApexPost.Application.Tests.Security;

public sealed class StaffPasswordHashingTests
{
	private const string Password = "Correct-Horse-Battery-1";

	[Fact]
	public void CreatedCredential_UsesTheSupportedWorkFactorAndVerifies()
	{
		var credential = StaffPasswordHasher.CreateCredential(" staff-1 ", Password);

		Assert.Equal("staff-1", credential.StaffId);
		Assert.Equal(StaffPasswordHashVerifier.MinimumIterations, credential.Iterations);
		Assert.True(StaffPasswordHashVerifier.Verify(Password, credential));
		Assert.False(StaffPasswordHashVerifier.Verify("wrong-password", credential));
		Assert.DoesNotContain(Password, credential.PasswordHash, StringComparison.Ordinal);
	}

	[Fact]
	public void SamePassword_GetsADifferentSaltEachTime()
	{
		var first = StaffPasswordHasher.CreateCredential("staff-1", Password);
		var second = StaffPasswordHasher.CreateCredential("staff-1", Password);

		Assert.NotEqual(first.Salt, second.Salt);
		Assert.NotEqual(first.PasswordHash, second.PasswordHash);
	}

	[Fact]
	public void CredentialBelowMinimumIterations_IsRejected()
	{
		var valid = StaffPasswordHasher.CreateCredential("staff-1", Password);
		var weak = valid with { Iterations = StaffPasswordHashVerifier.MinimumIterations - 1 };

		Assert.Throws<InvalidDataException>(
			() => StaffPasswordHashVerifier.Verify(Password, weak));
	}

	// The unknown-account path must cost about as much as a real check, so
	// response time does not reveal which staff IDs exist. Each is timed
	// several times and the fastest run compared, which keeps the check
	// stable on a busy machine; the old 210,000-iteration dummy took about
	// a third of the time of a real check.
	[Fact]
	public void DummyVerification_TakesAboutAsLongAsARealCheck()
	{
		var credential = StaffPasswordHasher.CreateCredential("staff-1", Password);

		var real = Fastest(() => StaffPasswordHashVerifier.Verify("wrong", credential));
		var dummy = Fastest(() => StaffPasswordHashVerifier.PerformDummyVerification("wrong"));

		Assert.InRange(dummy.TotalMilliseconds / real.TotalMilliseconds, 0.6, 1.6);
	}

	private static TimeSpan Fastest(Action action)
	{
		var fastest = TimeSpan.MaxValue;
		for (var attempt = 0; attempt < 3; attempt++)
		{
			var started = System.Diagnostics.Stopwatch.GetTimestamp();
			action();
			var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(started);
			if (elapsed < fastest)
				fastest = elapsed;
		}
		return fastest;
	}
}
