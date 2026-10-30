using ApexPost.Application.Verification;

namespace ApexPost.Infrastructure.Verification;

// Stands in for an external identity-verification provider. A document passes
// when its type is in the configured accepted list. The provider call is
// bounded by a timeout; a timeout is reported as "unavailable", separately
// from a caller cancellation, which is passed through unchanged.
public sealed class SimulatedIdentityVerificationService : IIdentityVerificationService
{
	private readonly HashSet<string> _acceptedDocumentTypes;
	private readonly TimeSpan _timeout;
	private readonly TimeSpan _simulatedLatency;
	private readonly TimeProvider _timeProvider;

	public SimulatedIdentityVerificationService(
		IEnumerable<string> acceptedDocumentTypes,
		TimeSpan timeout,
		TimeSpan simulatedLatency,
		TimeProvider timeProvider)
	{
		ArgumentNullException.ThrowIfNull(acceptedDocumentTypes);
		ArgumentNullException.ThrowIfNull(timeProvider);
		if (timeout <= TimeSpan.Zero)
			throw new ArgumentOutOfRangeException(
				nameof(timeout), "The verification timeout must be positive.");
		if (simulatedLatency < TimeSpan.Zero)
			throw new ArgumentOutOfRangeException(
				nameof(simulatedLatency), "Simulated latency cannot be negative.");

		_acceptedDocumentTypes = new HashSet<string>(
			StringComparer.OrdinalIgnoreCase);
		foreach (var documentType in acceptedDocumentTypes)
		{
			if (string.IsNullOrWhiteSpace(documentType))
				throw new ArgumentException(
					"Accepted document types cannot be blank.",
					nameof(acceptedDocumentTypes));
			_acceptedDocumentTypes.Add(documentType.Trim());
		}
		if (_acceptedDocumentTypes.Count == 0)
			throw new ArgumentException(
				"At least one accepted document type is required.",
				nameof(acceptedDocumentTypes));

		_timeout = timeout;
		_simulatedLatency = simulatedLatency;
		_timeProvider = timeProvider;
	}

	public async Task<bool> VerifyAsync(
		string documentType,
		string documentIdentifier,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(documentType);
		ArgumentException.ThrowIfNullOrWhiteSpace(documentIdentifier);

		using var timeoutSource = new CancellationTokenSource(
			_timeout, _timeProvider);
		using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
			cancellationToken, timeoutSource.Token);
		try
		{
			await SimulateProviderCallAsync(linkedSource.Token)
				.ConfigureAwait(false);
		}
		catch (OperationCanceledException exception) when (
			timeoutSource.IsCancellationRequested &&
			!cancellationToken.IsCancellationRequested)
		{
			throw new IdentityVerificationUnavailableException(
				"The identity verification provider did not respond in time.",
				exception);
		}

		return _acceptedDocumentTypes.Contains(documentType.Trim());
	}

	private Task SimulateProviderCallAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return _simulatedLatency == TimeSpan.Zero
			? Task.CompletedTask
			: Task.Delay(_simulatedLatency, _timeProvider, cancellationToken);
	}
}
