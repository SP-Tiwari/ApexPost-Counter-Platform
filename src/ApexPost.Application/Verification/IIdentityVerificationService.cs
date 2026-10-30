namespace ApexPost.Application.Verification;

public interface IIdentityVerificationService
{
	// Returns the verification result. The full document identifier is used
	// only for this check and must never be stored or logged.
	Task<bool> VerifyAsync(
		string documentType,
		string documentIdentifier,
		CancellationToken cancellationToken = default);
}

// Thrown when the verification provider cannot give a result (for example,
// it timed out). This is a failure to verify, not a failed verification.
public sealed class IdentityVerificationUnavailableException : Exception
{
	public IdentityVerificationUnavailableException(string message)
		: base(message)
	{
	}

	public IdentityVerificationUnavailableException(
		string message,
		Exception innerException)
		: base(message, innerException)
	{
	}
}
