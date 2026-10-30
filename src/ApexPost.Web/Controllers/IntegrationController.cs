using System.Text.RegularExpressions;
using ApexPost.Application.Courier;

namespace ApexPost.Web.Controllers;

// Courier-partner API. Staff cookies are not used: ApiKeyAndIpRestriction
// middleware authenticates the partner from the [RequireApiKey] metadata
// (X-API-Key, allowed IPs) and the partner rate limit applies afterwards.
// CORS for the partner tracking widget comes from the ParcelTracking policy.
[ApiController]
[AllowAnonymous]
[RequireApiKey]
[Route("integration")]
public sealed partial class IntegrationController(
	ParcelStatusApplicationService parcels) : ControllerBase
{
	[HttpGet("parcel-status/{shipmentId}")]
	public async Task<IActionResult> ParcelStatus(
		string shipmentId,
		CancellationToken cancellationToken)
	{
		if (!ShipmentIdPattern().IsMatch(shipmentId))
		{
			return BadRequest(new ProblemDetails
			{
				Title = "Invalid shipment ID.",
				Status = StatusCodes.Status400BadRequest
			});
		}

		var status = await parcels.GetStatusAsync(shipmentId, cancellationToken);
		if (status is null)
			return NotFound();

		return Ok(new
		{
			status.ShipmentId,
			Type = status.Type.ToString(),
			ServiceLevel = status.ServiceLevel.ToString(),
			status.DestinationZone,
			status.AcceptedAtUtc,
			TransactionStatus = status.TransactionStatus.ToString(),
			Courier = status.Courier is { } courier
				? new
				{
					Outcome = courier.Outcome.ToString(),
					Status = courier.Outcome == CourierLookupOutcome.Available
						? courier.Status
						: null
				}
				: new { Outcome = "NotConfigured", Status = (string?)null }
		});
	}

	// Shipment IDs are generated hex GUIDs; this bounds and sanitizes input.
	[GeneratedRegex("^[A-Za-z0-9-]{1,64}$")]
	private static partial Regex ShipmentIdPattern();
}
