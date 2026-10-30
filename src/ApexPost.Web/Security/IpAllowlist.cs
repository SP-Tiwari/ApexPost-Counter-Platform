using System.Net;

namespace ApexPost.Web.Security;

// An exact-address allowlist read from configuration. An empty list means the
// restriction is disabled, matching the existing ApiSecurity behaviour.
//
// The remote address is HttpContext.Connection.RemoteIpAddress. Behind a
// reverse proxy that is the proxy's address unless forwarded headers are
// configured to trust only that proxy; forwarded headers are not enabled here.
public sealed class IpAllowlist
{
	private readonly HashSet<IPAddress> _addresses;

	private IpAllowlist(HashSet<IPAddress> addresses) => _addresses = addresses;

	public bool IsEnabled => _addresses.Count > 0;

	public static IpAllowlist FromConfiguration(
		IConfiguration configuration,
		string sectionKey)
	{
		ArgumentNullException.ThrowIfNull(configuration);

		var configuredAddresses = configuration
			.GetSection(sectionKey)
			.Get<string[]>() ?? [];
		var addresses = new HashSet<IPAddress>();
		foreach (var configuredAddress in configuredAddresses)
		{
			if (!IPAddress.TryParse(configuredAddress, out var address))
			{
				throw new InvalidOperationException(
					$"{sectionKey} must contain valid IP address values.");
			}

			addresses.Add(Normalize(address));
		}

		return new IpAllowlist(addresses);
	}

	public bool Allows(IPAddress? remoteAddress) =>
		remoteAddress is not null && _addresses.Contains(Normalize(remoteAddress));

	private static IPAddress Normalize(IPAddress address) =>
		address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
}
