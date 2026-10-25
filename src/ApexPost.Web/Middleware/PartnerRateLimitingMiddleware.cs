using System.Threading.RateLimiting;

namespace ApexPost.Web.Middleware;

public sealed class PartnerRateLimitingMiddleware(
	RequestDelegate next,
	PartitionedRateLimiter<HttpContext> limiter)
{
	public const string PartnerIdentityItemKey =
		"ApexPost.ValidatedPartnerIdentity";

	public async Task InvokeAsync(HttpContext context)
	{
		if (!context.Items.ContainsKey(PartnerIdentityItemKey))
		{
			await next(context).ConfigureAwait(false);
			return;
		}

		using var lease = await limiter.AcquireAsync(
			context, 1, context.RequestAborted).ConfigureAwait(false);
		if (!lease.IsAcquired)
		{
			context.Response.StatusCode =
				StatusCodes.Status429TooManyRequests;
			return;
		}

		await next(context).ConfigureAwait(false);
	}
}
