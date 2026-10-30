using ApexPost.Application.Courier;

namespace ApexPost.Infrastructure.Courier;

// Stands in for a courier partner's tracking API. It returns the configured
// status, after an optional simulated latency.
// - A SemaphoreSlim bounds how many lookups run against the courier at once.
// - Each lookup, including its wait for a slot, is bounded by a timeout.
// - A timeout becomes TimedOut and an unexpected error becomes Failed;
//   cancellation by the caller is passed through unchanged.
public sealed class SimulatedCourierTrackingService
	: ICourierTrackingService, IDisposable
{
	private readonly SemaphoreSlim _concurrencyLimit;
	private readonly string _simulatedStatus;
	private readonly TimeSpan _timeout;
	private readonly TimeSpan _simulatedLatency;
	private readonly TimeProvider _timeProvider;

	public SimulatedCourierTrackingService(
		string simulatedStatus,
		int maximumConcurrentLookups,
		TimeSpan timeout,
		TimeSpan simulatedLatency,
		TimeProvider timeProvider)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(simulatedStatus);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumConcurrentLookups);
		ArgumentNullException.ThrowIfNull(timeProvider);
		if (timeout <= TimeSpan.Zero)
			throw new ArgumentOutOfRangeException(
				nameof(timeout), "The courier timeout must be positive.");
		if (simulatedLatency < TimeSpan.Zero)
			throw new ArgumentOutOfRangeException(
				nameof(simulatedLatency), "Simulated latency cannot be negative.");

		_simulatedStatus = simulatedStatus.Trim();
		_concurrencyLimit = new SemaphoreSlim(
			maximumConcurrentLookups, maximumConcurrentLookups);
		_timeout = timeout;
		_simulatedLatency = simulatedLatency;
		_timeProvider = timeProvider;
	}

	public async Task<CourierTrackingResult> GetTrackingAsync(
		string shipmentId,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(shipmentId);

		using var timeoutSource = new CancellationTokenSource(
			_timeout, _timeProvider);
		using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
			cancellationToken, timeoutSource.Token);
		var acquired = false;
		try
		{
			await _concurrencyLimit.WaitAsync(linkedSource.Token)
				.ConfigureAwait(false);
			acquired = true;
			if (_simulatedLatency > TimeSpan.Zero)
			{
				await Task.Delay(
					_simulatedLatency, _timeProvider, linkedSource.Token)
					.ConfigureAwait(false);
			}

			return new CourierTrackingResult(
				shipmentId, CourierLookupOutcome.Available, _simulatedStatus);
		}
		catch (OperationCanceledException) when (
			timeoutSource.IsCancellationRequested &&
			!cancellationToken.IsCancellationRequested)
		{
			return new CourierTrackingResult(
				shipmentId, CourierLookupOutcome.TimedOut, null);
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			return new CourierTrackingResult(
				shipmentId, CourierLookupOutcome.Failed, null);
		}
		finally
		{
			if (acquired)
				_concurrencyLimit.Release();
		}
	}

	public void Dispose() => _concurrencyLimit.Dispose();
}
