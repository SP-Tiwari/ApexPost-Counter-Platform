namespace ApexPost.Application.Courier;

public enum CourierLookupOutcome
{
	Available,
	TimedOut,
	Failed
}

// The courier's answer for one shipment. A timeout or a provider failure is a
// result, not an exception, so one slow lookup cannot fail a whole page;
// caller cancellation is still thrown as OperationCanceledException.
public sealed record CourierTrackingResult(
	string ShipmentId,
	CourierLookupOutcome Outcome,
	string? Status);

public interface ICourierTrackingService
{
	Task<CourierTrackingResult> GetTrackingAsync(
		string shipmentId,
		CancellationToken cancellationToken = default);
}
