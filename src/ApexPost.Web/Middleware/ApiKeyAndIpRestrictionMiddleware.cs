using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace ApexPost.Web.Middleware;

public sealed class ApiKeyAndIpRestrictionMiddleware
{
	private const string ApiKeyHeaderName = "X-API-Key";
	private readonly RequestDelegate _next;
	private readonly PathString[] _protectedPathPrefixes;
	private readonly byte[]? _expectedApiKeyHash;
	private readonly HashSet<IPAddress> _allowedIpAddresses;

	public ApiKeyAndIpRestrictionMiddleware(
		RequestDelegate next,
		IConfiguration configuration)
	{
		_next = next;
		_protectedPathPrefixes = ReadPathPrefixes(configuration);

		var expectedApiKey = configuration["ApiSecurity:ApiKey"];
		_expectedApiKeyHash = string.IsNullOrWhiteSpace(expectedApiKey)
			? null
			: SHA256.HashData(Encoding.UTF8.GetBytes(expectedApiKey));

		_allowedIpAddresses = ReadAllowedIpAddresses(configuration);
	}

	public async Task InvokeAsync(HttpContext context)
	{
		if (!_protectedPathPrefixes.Any(prefix =>
			context.Request.Path.StartsWithSegments(
				prefix, StringComparison.OrdinalIgnoreCase)))
		{
			await _next(context);
			return;
		}

		if (_expectedApiKeyHash is null)
		{
			context.Response.StatusCode =
				StatusCodes.Status500InternalServerError;
			return;
		}

		if (_allowedIpAddresses.Count > 0 &&
			(context.Connection.RemoteIpAddress is not { } remoteAddress ||
				!_allowedIpAddresses.Contains(NormalizeAddress(remoteAddress))))
		{
			context.Response.StatusCode = StatusCodes.Status403Forbidden;
			return;
		}

		if (!HasValidApiKey(context.Request, _expectedApiKeyHash))
		{
			context.Response.StatusCode = StatusCodes.Status401Unauthorized;
			return;
		}

		context.Items[PartnerRateLimitingMiddleware.PartnerIdentityItemKey] =
			Convert.ToHexString(_expectedApiKeyHash);

		await _next(context);
	}

	private static PathString[] ReadPathPrefixes(
		IConfiguration configuration)
	{
		var configuredPrefixes = configuration
			.GetSection("ApiSecurity:ProtectedPathPrefixes")
			.Get<string[]>() ?? [];

		return configuredPrefixes.Select(prefix =>
		{
			if (string.IsNullOrWhiteSpace(prefix) ||
				!prefix.StartsWith("/", StringComparison.Ordinal) ||
				prefix.StartsWith("//", StringComparison.Ordinal) ||
				prefix.Contains('?') ||
				prefix.Contains('#') ||
				prefix.Contains('\\'))
			{
				throw new InvalidOperationException(
					"API security path prefixes must be absolute URL paths.");
			}

			var normalizedPrefix = prefix.TrimEnd('/');
			if (normalizedPrefix.Length == 0)
			{
				throw new InvalidOperationException(
					"The root path cannot be configured as an API security prefix.");
			}

			return new PathString(normalizedPrefix);
		}).ToArray();
	}

	private static HashSet<IPAddress> ReadAllowedIpAddresses(
		IConfiguration configuration)
	{
		var configuredAddresses = configuration
			.GetSection("ApiSecurity:AllowedIpAddresses")
			.Get<string[]>() ?? [];
		var addresses = new HashSet<IPAddress>();

		foreach (var configuredAddress in configuredAddresses)
		{
			if (!IPAddress.TryParse(configuredAddress, out var address))
			{
				throw new InvalidOperationException(
					"API security allowed IP addresses must be valid IP address values.");
			}

			addresses.Add(NormalizeAddress(address));
		}

		return addresses;
	}

	private static IPAddress NormalizeAddress(IPAddress address) =>
		address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

	private static bool HasValidApiKey(
		HttpRequest request,
		byte[] expectedApiKeyHash)
	{
		if (!request.Headers.TryGetValue(ApiKeyHeaderName, out var suppliedKeys) ||
			suppliedKeys.Count != 1 ||
			string.IsNullOrEmpty(suppliedKeys[0]))
		{
			return false;
		}

		var suppliedHash = SHA256.HashData(
			Encoding.UTF8.GetBytes(suppliedKeys[0]!));
		return CryptographicOperations.FixedTimeEquals(
			expectedApiKeyHash, suppliedHash);
	}
}
